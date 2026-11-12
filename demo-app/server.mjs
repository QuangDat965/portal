import http from 'node:http'
import crypto from 'node:crypto'

const PORT = Number(process.env.PORT || 5174)
const PORTAL_URL = (process.env.PORTAL_URL || 'http://localhost:5080').replace(/\/$/, '')
const CLIENT_ID = process.env.CLIENT_ID || 'demo'
const CLIENT_SECRET = process.env.CLIENT_SECRET || 'demo-secret-change-me'

let jwks = null

function escapeHtml(value) {
  return String(value ?? '')
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
}

async function exchangeCode(code) {
  const response = await fetch(`${PORTAL_URL}/api/auth/token`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      clientId: CLIENT_ID,
      clientSecret: CLIENT_SECRET,
      code,
    }),
  })

  if (!response.ok) {
    throw new Error(`Đổi code thất bại (${response.status}).`)
  }

  return response.json()
}

async function verifyAccessToken(token) {
  const [headerPart, payloadPart, signaturePart] = token.split('.')
  if (!headerPart || !payloadPart || !signaturePart) {
    throw new Error('JWT không hợp lệ.')
  }

  const header = JSON.parse(Buffer.from(headerPart, 'base64url').toString())
  const payload = JSON.parse(Buffer.from(payloadPart, 'base64url').toString())

  if (!jwks) {
    const response = await fetch(`${PORTAL_URL}/api/.well-known/jwks.json`)
    if (!response.ok) throw new Error('Không tải được JWKS.')
    jwks = await response.json()
  }

  const jwk = jwks.keys.find((key) => key.kid === header.kid)
  if (!jwk) throw new Error('Không tìm thấy key ký JWT.')

  const publicKey = crypto.createPublicKey({ key: jwk, format: 'jwk' })
  const valid = crypto.verify(
    'RSA-SHA256',
    Buffer.from(`${headerPart}.${payloadPart}`),
    publicKey,
    Buffer.from(signaturePart, 'base64url'),
  )

  if (!valid) throw new Error('Chữ ký JWT không hợp lệ.')
  if (payload.exp * 1000 < Date.now()) throw new Error('JWT đã hết hạn.')

  return payload
}

function page(title, body) {
  return `<!doctype html>
<html lang="vi">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>${escapeHtml(title)}</title>
  <style>
    :root { color-scheme: dark; }
    * { box-sizing: border-box; }
    body {
      margin: 0; min-height: 100vh; display: grid; place-items: center; padding: 24px;
      font-family: ui-sans-serif, system-ui, -apple-system, "Segoe UI", sans-serif;
      color: #e2e8f0;
      background:
        radial-gradient(50rem 30rem at 20% -10%, rgba(56,189,248,.18), transparent 60%),
        radial-gradient(40rem 30rem at 90% 0%, rgba(129,140,248,.14), transparent 55%),
        #070b16;
    }
    .card {
      width: min(680px, 100%); border: 1px solid rgba(255,255,255,.1);
      background: rgba(15,23,42,.6); border-radius: 20px; padding: 28px;
      box-shadow: 0 24px 60px rgba(0,0,0,.35); backdrop-filter: blur(12px);
    }
    h1 { margin: 0 0 6px; font-size: 24px; }
    p { color: #94a3b8; line-height: 1.6; }
    a.button {
      display: inline-flex; align-items: center; gap: 8px; margin-top: 8px;
      padding: 10px 16px; border-radius: 10px; background: #38bdf8; color: #020617;
      font-weight: 700; text-decoration: none;
    }
    a.button.secondary {
      background: transparent; color: #cbd5e1; border: 1px solid rgba(255,255,255,.15);
    }
    .badge {
      display: inline-block; padding: 3px 10px; border-radius: 999px; font-size: 12px;
      background: rgba(56,189,248,.12); color: #7dd3fc; border: 1px solid rgba(56,189,248,.25);
    }
    dl { display: grid; grid-template-columns: 130px 1fr; gap: 12px 16px; margin: 20px 0 0; }
    dt { color: #64748b; font-size: 13px; }
    dd { margin: 0; color: #e2e8f0; font-size: 14px; word-break: break-word; }
    code { font-family: ui-monospace, monospace; font-size: 12px; color: #bae6fd; }
    .ok { color: #6ee7b7; }
    .err { color: #fda4af; }
    .row { display: flex; flex-wrap: wrap; gap: 8px; margin-top: 20px; }
  </style>
</head>
<body>
  <main class="card">${body}</main>
</body>
</html>`
}

function homePage() {
  return page(
    'Demo App · Portal SSO',
    `<span class="badge">Demo app</span>
     <h1 style="margin-top:14px">Ứng dụng mẫu</h1>
     <p>
       App này không có trang đăng nhập riêng. Người dùng đăng nhập ở portal,
       bấm mở app, rồi app đổi <code>launch code</code> lấy JWT và xác minh chữ ký bằng JWKS.
     </p>
     <div class="row">
       <a class="button" href="${escapeHtml(PORTAL_URL)}/apps">Mở portal để chọn app →</a>
       <a class="button secondary" href="${escapeHtml(PORTAL_URL)}/login">Đăng nhập portal</a>
     </div>`,
  )
}

function successPage(payload) {
  const roles = Array.isArray(payload.role) ? payload.role : [payload.role].filter(Boolean)

  return page(
    'Demo App · Đã đăng nhập',
    `<span class="badge ok">SSO thành công</span>
     <h1 style="margin-top:14px">Xin chào, ${escapeHtml(payload.name)}</h1>
     <p>JWT đã được xác minh bằng public key từ <code>/api/.well-known/jwks.json</code>.</p>
     <dl>
       <dt>Subject</dt><dd><code>${escapeHtml(payload.sub)}</code></dd>
       <dt>Email</dt><dd>${escapeHtml(payload.email)}</dd>
       <dt>Roles</dt><dd>${roles.map((role) => `<span class="badge">${escapeHtml(role)}</span>`).join(' ')}</dd>
       <dt>App</dt><dd><code>${escapeHtml(payload.app_slug)}</code></dd>
       <dt>Audience</dt><dd><code>${escapeHtml(payload.aud)}</code></dd>
       <dt>Hết hạn</dt><dd>${new Date(payload.exp * 1000).toLocaleString('vi-VN')}</dd>
     </dl>
     <div class="row">
       <a class="button" href="${escapeHtml(PORTAL_URL)}/apps">Về portal</a>
     </div>`,
  )
}

function errorPage(message) {
  return page(
    'Demo App · Lỗi',
    `<span class="badge err">Không đăng nhập được</span>
     <h1 style="margin-top:14px">Có lỗi xảy ra</h1>
     <p>${escapeHtml(message)}</p>
     <div class="row"><a class="button" href="${escapeHtml(PORTAL_URL)}/apps">Về portal</a></div>`,
  )
}

const server = http.createServer(async (request, response) => {
  const url = new URL(request.url ?? '/', `http://localhost:${PORT}`)

  if (url.pathname === '/health') {
    response.writeHead(200, { 'Content-Type': 'text/plain' })
    response.end('ok')
    return
  }

  if (url.pathname === '/auth/callback') {
    const code = url.searchParams.get('code')
    if (!code) {
      response.writeHead(400, { 'Content-Type': 'text/html; charset=utf-8' })
      response.end(errorPage('Thiếu launch code.'))
      return
    }

    try {
      const tokens = await exchangeCode(code)
      const payload = await verifyAccessToken(tokens.accessToken)
      response.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8' })
      response.end(successPage(payload))
    } catch (error) {
      response.writeHead(400, { 'Content-Type': 'text/html; charset=utf-8' })
      response.end(errorPage(error instanceof Error ? error.message : String(error)))
    }
    return
  }

  if (url.pathname === '/') {
    response.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8' })
    response.end(homePage())
    return
  }

  response.writeHead(404, { 'Content-Type': 'text/html; charset=utf-8' })
  response.end(errorPage('Không tìm thấy trang.'))
})

server.listen(PORT, () => {
  console.log(`Demo app listening on http://localhost:${PORT} (portal: ${PORTAL_URL})`)
})
