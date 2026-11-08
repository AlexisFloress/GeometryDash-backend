import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';

// Run against a fresh disposable PostgreSQL database and a newly started API.
const base = process.env.GD_API_URL ?? 'http://localhost:5080';
const origin = new URL(base).origin;
const suffix = randomUUID().replaceAll('-', '').slice(0, 12);
const password = 'TestGeometryDash123';
let passed = 0;
async function request(path, { token, method = 'GET', body, headers = {}, status = 200 } = {}) {
  const response = await fetch(base + '/api/v1' + path, {
    method, headers: { Origin: origin, ...(token ? { Authorization: `Bearer ${token}` } : {}), ...(body ? { 'Content-Type': 'application/json' } : {}), ...headers },
    body: body ? JSON.stringify(body) : undefined,
  });
  const text = await response.text();
  let data; try { data = text ? JSON.parse(text) : null; } catch { data = text; }
  assert.equal(response.status, status, `${method} ${path}: ${response.status}, esperado ${status}; code=${data?.code ?? ''}`);
  return { data, headers: response.headers };
}
function check(name, condition = true) { assert.ok(condition, name); passed++; console.log('OK:', name); }
const account = (name) => ({ userName: name + suffix, displayName: name, email: `${name}${suffix}@example.com`, password });
const owner = account('autor'); const player = account('jugador');
const duplicate = await Promise.all([owner, owner].map(async body => {
  const r = await fetch(base + '/api/v1/auth/register', { method: 'POST', headers: { Origin: origin, 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
  return r.status;
}));
check('Registro concurrente único (AC01)', duplicate.filter(s => s === 201).length === 1 && duplicate.some(s => s === 409 || s === 422));
await request('/auth/register', { method: 'POST', body: player, status: 201 });
const a = (await request('/auth/login', { method: 'POST', body: { email: owner.email, password } })).data;
const b = (await request('/auth/login', { method: 'POST', body: { email: player.email, password } })).data;
check('La sesión no expone hashes', !JSON.stringify(a).includes('passwordHash') && !JSON.stringify(a).includes('refreshTokenHash'));
const createKey = randomUUID(); const createBody = { title: 'Integración ' + suffix, description: '' };
let draft = (await request('/levels', { token: a.accessToken, method: 'POST', body: createBody, headers: { 'Idempotency-Key': createKey }, status: 201 })).data;
const level = draft.levelId;
const again = (await request('/levels', { token: a.accessToken, method: 'POST', body: createBody, headers: { 'Idempotency-Key': createKey } })).data;
check('Reintentar creación devuelve el mismo nivel', again.levelId === level);
await request(`/levels/${level}/draft`, { token: b.accessToken, status: 404 }); check('Borrador privado (AC02)');
await request(`/levels/${level}/publish`, { token: a.accessToken, method: 'POST', body: { draftRevisionId: draft.revisionId, expectedLevelVersion: draft.levelVersion }, headers: { 'If-Match': '"1"' }, status: 422 }); check('No publicar vacío (AC04)');
let body = { title: draft.title, description: '', difficulty: 'Normal', content: { ...draft.content, objects: [{ id: 'obj_01', type: 'triangle', x: 20, y: 0, width: 1, height: 1, rotation: 0 }] } };
await request(`/levels/${level}/draft`, { token: a.accessToken, method: 'PUT', body, status: 428 }); check('Se requiere If-Match');
const saves = await Promise.all([1, 2].map(async () => {
  const r = await fetch(`${base}/api/v1/levels/${level}/draft`, { method: 'PUT', headers: { Authorization: `Bearer ${a.accessToken}`, 'Content-Type': 'application/json', 'If-Match': '"1"' }, body: JSON.stringify(body) }); return r.status;
}));
check('Concurrencia de borradores (AC03)', saves.includes(200) && saves.includes(412));
draft = (await request(`/levels/${level}/draft`, { token: a.accessToken })).data;
await request(`/levels/${level}/draft`, { token: a.accessToken, method: 'PUT', body: { ...body, content: { ...body.content, objects: [{ ...body.content.objects[0], type: 'script' }] } }, headers: { 'If-Match': `"${draft.version}"` }, status: 422 }); check('Tipo desconocido rechazado (AC05)');
await request(`/levels/${level}/publish`, { token: a.accessToken, method: 'POST', body: { draftRevisionId: draft.revisionId, expectedLevelVersion: draft.levelVersion }, headers: { 'If-Match': `"${draft.version}"` } });
let card = (await request(`/levels/${level}`)).data;
check('Sin votos devuelve null y cero (AC20)', card.averageRating === null && card.ratingCount === 0);
const rev = card.revisionId;
await request(`/levels/${level}/favorite`, { token: b.accessToken, method: 'PUT', status: 204 });
await request(`/levels/${level}/favorite`, { token: b.accessToken, method: 'PUT', status: 204 });
check('Favorito único (AC08)', (await request('/me/favorites', { token: b.accessToken })).data.items.filter(x => x.id === level).length === 1);
await request(`/revisions/${rev}/rating`, { token: a.accessToken, method: 'PUT', body: { score: 5 }, status: 403 }); check('Autor no puede votar (AC11)');
await request(`/revisions/${rev}/rating`, { token: b.accessToken, method: 'PUT', body: { score: 5 }, status: 422 }); check('Requiere intento finalizado');
const attempt = (await request(`/levels/${level}/attempts`, { token: b.accessToken, method: 'POST', body: { expectedRevisionId: rev }, status: 201 })).data;
await request(`/attempts/${attempt.id}`, { token: a.accessToken, status: 404 }); check('Intento privado');
const result = { clientResultId: randomUUID(), outcome: 'Quit', elapsedTicks: 0, pauseTicks: 0, progressPercent: 0 };
await request(`/attempts/${attempt.id}/result`, { token: b.accessToken, method: 'PUT', body: result });
await request(`/attempts/${attempt.id}/result`, { token: b.accessToken, method: 'PUT', body: result }); check('Cierre idempotente (AC13)');
await request(`/attempts/${attempt.id}/result`, { token: b.accessToken, method: 'PUT', body: { ...result, clientResultId: randomUUID() }, status: 409 }); check('Cierre diferente rechazado (AC14)');
await request(`/revisions/${rev}/rating`, { token: b.accessToken, method: 'PUT', body: { score: 2 }, status: 204 });
await request(`/revisions/${rev}/rating`, { token: b.accessToken, method: 'PUT', body: { score: 5 }, status: 204 });
card = (await request(`/levels/${level}`)).data;
check('Editar nota mantiene un voto (AC12)', card.averageRating === 5 && card.ratingCount === 1);
draft = (await request(`/levels/${level}/draft`, { token: a.accessToken, method: 'POST' })).data;
body = { ...body, title: 'Revisión dos ' + suffix, content: { ...body.content, speedMultiplier: 1.5 } };
draft = (await request(`/levels/${level}/draft`, { token: a.accessToken, method: 'PUT', body, headers: { 'If-Match': `"${draft.version}"` } })).data;
check('Borrador conserva publicación anterior (AC06)', (await request(`/levels/${level}`)).data.revisionId === rev);
await request(`/levels/${level}/publish`, { token: a.accessToken, method: 'POST', body: { draftRevisionId: draft.revisionId, expectedLevelVersion: draft.levelVersion }, headers: { 'If-Match': `"${draft.version}"` } });
card = (await request(`/levels/${level}`)).data;
check('Nueva revisión no hereda votos', card.revisionId !== rev && card.averageRating === null);
check('Favorito sobrevive republicación (AC07)', (await request('/me/favorites', { token: b.accessToken })).data.items.some(x => x.id === level));
const race = await Promise.all([
  fetch(`${base}/api/v1/levels/${level}/favorite`, { method: 'PUT', headers: { Authorization: `Bearer ${b.accessToken}` } }),
  fetch(`${base}/api/v1/levels/${level}`, { method: 'DELETE', headers: { Authorization: `Bearer ${a.accessToken}` } }),
]);
check('Carrera favorito/eliminación termina sin error interno (AC10)', [204, 404].includes(race[0].status) && race[1].status === 204);
check('Eliminación limpia favoritos (AC09)', !(await request('/me/favorites', { token: b.accessToken })).data.items.some(x => x.id === level));
await request(`/levels/${level}`, { status: 404 });
await request(`/levels/${level}`, { token: a.accessToken, method: 'DELETE', status: 204 }); check('Eliminar es idempotente');
console.log(`${passed} comprobaciones de API aprobadas. Se conservaron dos cuentas de prueba; usa una base desechable.`);
