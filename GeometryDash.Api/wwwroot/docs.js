const $ = (id) => document.getElementById(id);
$('server').textContent = window.location.origin;
async function loadOperations() {
  try {
    const result = await fetch('/openapi/v1.json');
    if (!result.ok) throw new Error('No se pudo cargar OpenAPI.');
    const spec = await result.json(); $('operations').textContent = '';
    for (const [path, methods] of Object.entries(spec.paths)) {
      for (const [method, operation] of Object.entries(methods)) {
        if (!['get', 'post', 'put', 'patch', 'delete'].includes(method)) continue;
        const article = document.createElement('article'); article.className = 'operation';
        const heading = document.createElement('h3');
        const verb = document.createElement('strong'); verb.textContent = method.toUpperCase();
        const label = document.createElement('code'); label.textContent = ' ' + path;
        heading.append(verb, label); article.append(heading);
        const guide = operation['x-request-guide'] ?? {};
        const example = operation.requestBody?.content?.['application/json']?.example;
        addText(article, 'p', operation.summary ?? '');
        addText(article, 'p', guide.access ?? '');
        const headers = Object.entries(guide.headers ?? {});
        if (headers.length) { addText(article, 'h4', 'Encabezados que debes enviar'); addText(article, 'pre', headers.map(([name, value]) => `${name}: ${value}`).join('\n')); }
        const parameters = (operation.parameters ?? []).filter(p => p.in === 'path' || p.in === 'query');
        if (parameters.length) {
          addText(article, 'h4', 'Datos de la ruta y filtros');
          const list = document.createElement('dl');
          for (const p of parameters) { addText(list, 'dt', p.name + (p.required ? ' (obligatorio)' : ' (opcional)')); addText(list, 'dd', (p.description ?? '') + (p.example !== undefined ? ' Ejemplo: ' + p.example : '')); }
          article.append(list);
        }
        addText(article, 'h4', 'JSON de la solicitud');
        if (example !== undefined) {
          addText(article, 'pre', JSON.stringify(example, null, 2));
          const fields = document.createElement('dl');
          for (const [name, description] of Object.entries(guide.fields ?? {})) { addText(fields, 'dt', name); addText(fields, 'dd', description); }
          article.append(fields);
        } else addText(article, 'p', 'No lleva cuerpo JSON. Deja vacío el campo de datos.');
        for (const note of guide.notes ?? []) addText(article, 'p', note);
        const button = document.createElement('button'); button.type = 'button'; button.textContent = 'Probar este endpoint';
        button.onclick = () => {
          $('method').value = method.toUpperCase(); $('path').value = path;
          $('body').value = example === undefined ? '' : JSON.stringify(example, null, 2);
          $('etag').value = guide.headers?.['If-Match'] ?? '';
          $('idem').value = guide.headers?.['Idempotency-Key'] ? crypto.randomUUID() : '';
          $('probar').scrollIntoView(); $('path').focus();
        };
        article.append(button); $('operations').append(article);
      }
    }
  } catch (error) { $('operations').textContent = error.message; }
}
function addText(parent, tag, value) {
  const element = document.createElement(tag); element.textContent = value; parent.append(element);
}
$('request').addEventListener('submit', async (event) => {
  event.preventDefault(); const button = event.submitter; button.disabled = true;
  try {
    const path = $('path').value.trim();
    const url = new URL(path, window.location.origin);
    if (url.origin !== window.location.origin || !(url.pathname.startsWith('/api/v1/') || ['/openapi/v1.json', '/health/live', '/health/ready', '/docs/backend'].includes(url.pathname))) throw new Error('Usa una ruta de la API o de la documentación de este servicio.');
    if (/[{}]/.test(path)) throw new Error('Reemplaza {id} por el identificador que quieres usar.');
    const headers = {}; const method = $('method').value;
    if ($('token').value.trim()) headers.Authorization = 'Bearer ' + $('token').value.trim();
    if ($('etag').value.trim()) headers['If-Match'] = $('etag').value.trim();
    if ($('idem').value.trim()) headers['Idempotency-Key'] = $('idem').value.trim();
    let body;
    if (method !== 'GET' && $('body').value.trim()) { body = JSON.stringify(JSON.parse($('body').value)); headers['Content-Type'] = 'application/json'; }
    const response = await fetch(url, { method, headers, body, credentials: 'same-origin' });
    const raw = await response.text(); let data;
    try { data = raw ? JSON.parse(raw) : null; } catch { data = raw; }
    if (response.ok && data?.accessToken) $('token').value = data.accessToken;
    if (response.ok && url.pathname === '/api/v1/auth/logout') $('token').value = '';
    if (response.headers.get('ETag')) $('etag').value = response.headers.get('ETag');
    const display = data?.accessToken ? { ...data, accessToken: '(copiado al campo Bearer)' } : data;
    $('response').textContent = `${response.status} ${response.statusText}\n${JSON.stringify(display, null, 2) ?? ''}`;
  } catch (error) { $('response').textContent = error.message; }
  finally { button.disabled = false; }
});
loadOperations();
