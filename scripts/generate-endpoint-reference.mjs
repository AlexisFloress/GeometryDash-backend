import { readFile, writeFile } from 'node:fs/promises';

if (!process.argv[2]) throw new Error('Uso: node scripts/generate-endpoint-reference.mjs archivo-openapi.json');
const spec = JSON.parse(await readFile(process.argv[2], 'utf8'));
let text = '# Cómo enviar solicitudes a la API\n\nBase pública: `https://geometrydash-api.onrender.com`. Añade a esa dirección la ruta del endpoint. Esta referencia se genera a partir de OpenAPI, igual que la documentación web.\n\nLos JSON son ejemplos: cambia correos, contraseñas e identificadores por tus datos. Los UUID de ejemplo no apuntan a recursos existentes. Envía solamente los campos indicados; los campos desconocidos se rechazan.\n\nPara usar tu cuenta, registra un usuario e inicia sesión. Copia el `accessToken` de login en `Authorization: Bearer TU_ACCESS_TOKEN`. Conserva las cookies para renovar o cerrar sesión. Login, refresh y logout requieren un `Origin` permitido por la configuración del servidor; los navegadores lo envían automáticamente. Los clientes externos deben enviarlo expresamente.\n\nNo envíes `{}` en los endpoints sin cuerpo. Los filtros de los listados se envían en la URL; por ejemplo: `/api/v1/levels?page=1&pageSize=20&difficulty=Normal&sort=recent`. Los parámetros de consulta son opcionales.\n\n';

for (const [path, methods] of Object.entries(spec.paths)) {
    for (const [method, operation] of Object.entries(methods)) {
        if (!['get', 'post', 'put', 'patch', 'delete'].includes(method)) continue;
        const guide = operation['x-request-guide'] ?? {};
        const example = operation.requestBody?.content?.['application/json']?.example;
        text += `## ${method.toUpperCase()} ${path}\n\n${operation.summary}\n\n${guide.access}\n\n`;
        const headers = Object.entries(guide.headers ?? {});

        if (headers.length) text += 'Encabezados:\n\n```http\n' + headers.map(([key, value]) => `${key}: ${value}`).join('\n') + '\n```\n\n';

        for (const p of operation.parameters ?? []) {
            if (p.in === 'path' || p.in === 'query') text += `- \`${p.name}\` (${p.in === 'path' ? 'ruta, obligatorio' : 'consulta, opcional'}): ${p.description}\n`;
        }
        text += '\n';

        if (example === undefined) text += 'No lleva cuerpo JSON. Deja vacío el campo de datos.\n\n';
        else {
            text += 'JSON de la solicitud:\n\n```json\n' + JSON.stringify(example, null, 2) + '\n```\n\n';

            for (const [name, description] of Object.entries(guide.fields ?? {})) text += `- \`${name}\`: ${description}\n`;
            text += '\n';
        }

        for (const note of guide.notes ?? []) text += note + '\n\n';
    }
}
await writeFile(new URL('../docs/solicitudes-api.md', import.meta.url), text);
console.log('Generada docs/solicitudes-api.md');
