import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, writeFileSync, unlinkSync } from 'node:fs';
import { resolve, join } from 'node:path';
import { createHash, randomBytes, randomUUID } from 'node:crypto';

const root = resolve(import.meta.dirname, '..');
const host = process.env.GD_PG_HOST || 'localhost';
const port = process.env.GD_PG_PORT || '5432';
const admin = process.env.GD_PG_ADMIN || 'postgres';
const database = process.env.GD_DB_NAME || 'geometrydash';
const user = process.env.GD_DB_USER || 'geometrydash';

for (const name of [database, user]) if (!/^[a-z][a-z0-9_]{0,62}$/.test(name)) throw new Error('El nombre de base/usuario debe usar minúsculas, números y guion bajo (máximo 63).');

if (!/^[a-zA-Z0-9._-]+$/.test(host) || !/^\d{1,5}$/.test(port) || Number(port) > 65535) throw new Error('Host o puerto no admitido.');
const psql = process.env.GD_PSQL || 'C:\\Program Files\\PostgreSQL\\18\\bin\\psql.exe';

if (!existsSync(psql)) throw new Error('No se encontró psql. Configura GD_PSQL con su ruta.');
const configPath = join(root, 'GeometryDash.Api/appsettings.Local.json');

if (existsSync(configPath)) throw new Error('Ya existe appsettings.Local.json. No se sobrescribió ni se modificó la base existente.');
const migration = readFileSync(join(root, 'GeometryDash.Api/Data/Migrations/001_initial.sql'), 'utf8').replace(/^\uFEFF/, '').replaceAll('\r\n', '\n');
const migrationHash = createHash('sha256').update(migration).digest('hex');
const password = randomBytes(36).toString('base64');
const configuration = {
    ConnectionStrings: {
        Database: `Host=${host};Port=${port};Database=${database};Username=${user};Password=${password}`
    },
    Jwt: {
        Key: randomBytes(48).toString('base64')
    }
};
const temporary = join(root, '.local', 'database-setup');
mkdirSync(temporary, {
    recursive: true
});
const nonce = randomUUID();
const sqlPath = join(temporary, `${nonce}.sql`);
const pendingConfig = join(temporary, `${nonce}.json`);
writeFileSync(pendingConfig, JSON.stringify(configuration, null, 2), {
    flag: 'wx',
    mode: 0o600
});
// Names are restricted above. Generated base64 secrets contain no SQL quote characters.
const sql = `
DO $setup$
BEGIN
 IF EXISTS (SELECT 1 FROM pg_database WHERE datname = '${database}') THEN RAISE EXCEPTION 'La base ya existe. No se modifico. Usa GD_DB_NAME para elegir otra.'; END IF;
 IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = '${user}') THEN RAISE EXCEPTION 'El rol ya existe. No se modifico. Usa GD_DB_USER para elegir otro.'; END IF;
END $setup$;
CREATE ROLE "${user}" LOGIN PASSWORD '${password}';
CREATE DATABASE "${database}" OWNER "${user}";
\\connect ${database}
SET ROLE "${user}";
BEGIN;
SELECT pg_advisory_xact_lock(712345601);
CREATE TABLE "__GeometryMigrations" ("Name" text PRIMARY KEY, "Hash" text NOT NULL, "AppliedAt" timestamptz NOT NULL DEFAULT now());
${migration}
INSERT INTO "__GeometryMigrations" ("Name", "Hash") VALUES ('GeometryDash.Api.Data.Migrations.001_initial.sql', '${migrationHash}');
INSERT INTO "AspNetRoles" ("Id", "Name", "NormalizedName", "ConcurrencyStamp") VALUES
 ('${randomUUID()}', 'User', 'USER', '${randomUUID()}'),
 ('${randomUUID()}', 'Administrator', 'ADMINISTRATOR', '${randomUUID()}');
COMMIT;
SELECT current_database() AS database, current_user AS owner;
SELECT count(*) AS tables_created FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE';
SELECT "Name" FROM "AspNetRoles" ORDER BY "Name";
`;
writeFileSync(sqlPath, sql, {
    flag: 'wx',
    mode: 0o600
});
console.log(`Se creará una base NUEVA: ${database}, con propietario ${user}. No se reemplazan bases ni roles existentes.`);
console.log(`PostgreSQL pedirá la contraseña administrativa de ${admin}. Se introduce en esta terminal y no se guarda en el proyecto.`);
let complete = false;

try {
    const result = spawnSync(
        psql,
        [
            '-X',
            '-W',
            '-v',
            'ON_ERROR_STOP=1',
            '-h',
            host,
            '-p',
            port,
            '-U',
            admin,
            '-d',
            'postgres',
            '-f',
            sqlPath
        ],
        {
            stdio: 'inherit',
            env: {
                ...process.env,
                PGCONNECT_TIMEOUT: '10'
            }
        }
    );

    if (result.error || result.status !== 0) throw new Error('PostgreSQL no completó la creación. Revisa el error anterior. No se eliminan bases ni roles automáticamente.');
    writeFileSync(configPath, JSON.stringify(configuration, null, 2) + '\n', {
        flag: 'wx',
        mode: 0o600
    });
    complete = true;
    unlinkSync(pendingConfig);
    console.log('Base, tablas, índices y roles de aplicación creados. Configuración local y clave JWT generadas.');
    console.log('appsettings.Local.json está excluido de Git. Resta ejecutar dotnet restore y arrancar la API.');
} finally {
    if (existsSync(sqlPath)) unlinkSync(sqlPath);

    if (!complete) console.error(`La configuración generada se conserva para recuperación en ${pendingConfig}. Contiene secretos: no la compartas.`);
}
