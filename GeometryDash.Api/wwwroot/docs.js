const $ = (id) => document.getElementById(id);
async function loadOperations() {
  try {
    const result = await fetch('/openapi/v1.json');
    if (!result.ok) throw new Error('No se pudo cargar OpenAPI.');
    const spec = await result.json(); $('operations').textContent = '';
    for (const [path, methods] of Object.entries(spec.paths)) {
      for (const [method, operation] of Object.entries(methods)) {
        if (!['get', 'post', 'put', 'patch', 'delete'].includes(method)) continue;
        const button = document.createElement('button'); button.type = 'button'; button.className = 'op';
        const verb = document.createElement('strong'); verb.textContent = method.toUpperCase();
        const label = document.createElement('span'); label.textContent = path + (operation.summary ? ' · ' + operation.summary : '');
        button.append(verb, label); button.onclick = () => { $('method').value = method.toUpperCase(); $('path').value = path; $('probar').scrollIntoView(); $('path').focus(); };
        $('operations').append(button);
      }
    }
  } catch (error) { $('operations').textContent = error.message; }
}
$('request').addEventListener('submit', async (event) => {
  event.preventDefault(); const button = event.submitter; button.disabled = true;
  try {
    const path = $('path').value.trim();
    if (!path.startsWith('/api/v1/') || path.includes('://')) throw new Error('Usa una ruta relativa de /api/v1/.');
    const headers = {}; const method = $('method').value;
    if ($('token').value.trim()) headers.Authorization = 'Bearer ' + $('token').value.trim();
    if ($('etag').value.trim()) headers['If-Match'] = $('etag').value.trim();
    if ($('idem').value.trim()) headers['Idempotency-Key'] = $('idem').value.trim();
    let body;
    if (method !== 'GET' && $('body').value.trim()) { body = JSON.stringify(JSON.parse($('body').value)); headers['Content-Type'] = 'application/json'; }
    const response = await fetch(path, { method, headers, body, credentials: 'same-origin' });
    const raw = await response.text(); let data;
    try { data = raw ? JSON.parse(raw) : null; } catch { data = raw; }
    if (data?.accessToken) $('token').value = data.accessToken;
    if (response.headers.get('ETag')) $('etag').value = response.headers.get('ETag');
    const display = data?.accessToken ? { ...data, accessToken: '(copiado al campo Bearer)' } : data;
    $('response').textContent = `${response.status} ${response.statusText}\n${JSON.stringify(display, null, 2) ?? ''}`;
  } catch (error) { $('response').textContent = error.message; }
  finally { button.disabled = false; }
});
loadOperations();
