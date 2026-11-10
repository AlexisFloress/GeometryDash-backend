import assert from 'node:assert/strict';
import { writeFile } from 'node:fs/promises';

// Read-only: does not create accounts, levels or results in the target API.
const url = process.env.GD_DOCS_URL ?? 'http://localhost:5080/openapi/v1.json';
const response = await fetch(url);
assert.equal(response.status, 200, 'OpenAPI debe ser público');
const spec = await response.json();
let count = 0;
let examples = 0;
function validate(value, schema, label) {
  if (schema.$ref) schema = schema.$ref.slice(2).split('/').reduce((node, key) => node[key], spec);
  for (const part of schema.allOf ?? []) validate(value, part, label);
  if (schema.type === 'object' || schema.properties) {
    assert.equal(typeof value, 'object', label);
    for (const key of schema.required ?? []) assert.ok(Object.hasOwn(value, key), `${label}.${key} es obligatorio`);
    for (const [key, child] of Object.entries(value)) {
      assert.ok(schema.properties?.[key], `${label}.${key} no existe en el contrato`);
      validate(child, schema.properties[key], `${label}.${key}`);
    }
  } else if (schema.type === 'array') {
    assert.ok(Array.isArray(value), label);
    value.forEach((child, i) => validate(child, schema.items, `${label}[${i}]`));
  } else if (schema.type === 'integer') assert.ok(Number.isInteger(value), label);
  else if (schema.type === 'number') assert.equal(typeof value, 'number', label);
  else if (schema.type === 'string') {
    assert.equal(typeof value, 'string', label);
    if (schema.format === 'uuid') assert.match(value, /^[\da-f]{8}-[\da-f]{4}-[\da-f]{4}-[\da-f]{4}-[\da-f]{12}$/i, label);
  }
  if (schema.enum) assert.ok(schema.enum.includes(value), label);
}
for (const [path, methods] of Object.entries(spec.paths)) {
  for (const [method, operation] of Object.entries(methods)) {
    if (!['get', 'post', 'put', 'patch', 'delete'].includes(method)) continue;
    count++;
    const label = `${method.toUpperCase()} ${path}`;
    const guide = operation['x-request-guide'];
    assert.ok(guide?.access, `${label}: falta acceso`);
    const media = operation.requestBody?.content?.['application/json'];
    if (media) {
      assert.ok(media.example, `${label}: falta JSON`);
      validate(media.example, media.schema, label);
      for (const field of Object.keys(media.example)) assert.ok(guide.fields[field], `${label}: falta explicar ${field}`);
      examples++;
    } else assert.ok(guide.notes.some(note => note.includes('no recibe un cuerpo JSON')), `${label}: debe indicar que no lleva JSON`);
    if (path.startsWith('/api/v1/admin/') || path === '/api/v1/me' || path.startsWith('/api/v1/me/'))
      assert.equal(guide.headers.Authorization, 'Bearer TU_ACCESS_TOKEN', `${label}: falta indicar sesión`);
    if (method === 'put' && path.endsWith('/draft') || method === 'post' && path.endsWith('/publish'))
      assert.ok(operation.parameters.some(p => p.name === 'If-Match' && p.required), label);
  }
}
assert.ok(count >= 32, 'Faltan operaciones');
assert.equal(examples, 13, 'Todas las operaciones que reciben JSON deben tener ejemplo');
if (process.argv[2]) await writeFile(process.argv[2], JSON.stringify(spec, null, 2));
console.log(`${count} operaciones documentadas; ${examples} ejemplos JSON compatibles con el contrato. Sin modificar datos.`);
