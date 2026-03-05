const express = require('express');
const cors = require('cors');
const dotenv = require('dotenv');

dotenv.config();

const app = express();
app.use(cors());
app.use(express.json());

const port = Number(process.env.PORT || 8787);
const mayaBaseUrl = (process.env.MAYA_BASE_URL || 'https://pg-sandbox.paymaya.com').replace(/\/$/, '');
const redirectSignals = new Map();
const requestRefToCheckout = new Map();

function toIsoNow() {
  return new Date().toISOString();
}

function buildBasicAuthHeader() {
  if (process.env.MAYA_BASIC_AUTH && process.env.MAYA_BASIC_AUTH.trim()) {
    return process.env.MAYA_BASIC_AUTH.trim();
  }

  const publicKey = process.env.MAYA_PUBLIC_KEY || '';
  const secretKey = process.env.MAYA_SECRET_KEY || '';
  if (!publicKey) {
    throw new Error('Missing Maya credentials. Set MAYA_BASIC_AUTH or MAYA_PUBLIC_KEY.');
  }

  const raw = `${publicKey}:${secretKey}`;
  const b64 = Buffer.from(raw, 'utf8').toString('base64');
  return `Basic ${b64}`;
}

function isPaidStatus(status) {
  const value = String(status || '').toUpperCase();
  return [
    'PAYMENT_SUCCESS',
    'PAYMENT_SUCCESSFUL',
    'PAID',
    'SUCCESS',
    'AUTHORIZED',
    'COMPLETED',
    'APPROVED',
    'CAPTURED'
  ].includes(value);
}

function escapeHtml(value) {
  return String(value || '')
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
}

function renderReturnPage({ title, message, type, req }) {
  const checkoutId = escapeHtml(req.query.checkoutId || req.query.id || '');
  return `<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>${escapeHtml(title)}</title>
  <style>
    :root { color-scheme: light; }
    body {
      margin: 0;
      font-family: Segoe UI, Arial, sans-serif;
      background: #f5f7fb;
      color: #1f2937;
      min-height: 100vh;
      display: grid;
      place-items: center;
      padding: 24px;
    }
    .card {
      width: min(680px, 100%);
      background: #ffffff;
      border-radius: 12px;
      border: 1px solid #e5e7eb;
      padding: 24px;
      box-shadow: 0 12px 30px rgba(15, 23, 42, 0.08);
    }
    .badge {
      display: inline-block;
      border-radius: 999px;
      padding: 6px 10px;
      font-size: 12px;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.04em;
      background: ${type === 'success' ? '#dcfce7' : type === 'cancel' ? '#fef3c7' : '#fee2e2'};
      color: ${type === 'success' ? '#166534' : type === 'cancel' ? '#92400e' : '#991b1b'};
    }
    h1 { margin: 12px 0 8px; font-size: 28px; }
    p { margin: 0 0 10px; line-height: 1.45; }
    code {
      background: #f3f4f6;
      border: 1px solid #e5e7eb;
      border-radius: 6px;
      padding: 2px 6px;
    }
  </style>
</head>
<body>
  <main class="card">
    <span class="badge">${escapeHtml(type)}</span>
    <h1>${escapeHtml(title)}</h1>
    <p>${escapeHtml(message)}</p>
    ${checkoutId ? `<p>Checkout ID: <code>${checkoutId}</code></p>` : ''}
    <p>You can now return to Unity. Payment confirmation and item delivery are checked automatically in-game.</p>
  </main>
</body>
</html>`;
}

function setRedirectSignal(req, signalStatus) {
  const directCheckoutId = String(req.query.checkoutId || req.query.id || '').trim();
  const rrn = String(req.query.rrn || req.query.reference || '').trim();
  const checkoutId = directCheckoutId || (rrn ? requestRefToCheckout.get(rrn) || '' : '');
  if (!checkoutId) {
    return;
  }

  redirectSignals.set(checkoutId, {
    status: signalStatus,
    paid: signalStatus === 'REDIRECT_SUCCESS',
    source: 'redirect',
    updatedAt: toIsoNow()
  });
}

app.get('/health', (_req, res) => {
  res.json({ ok: true, mayaBaseUrl });
});

app.get('/maya-return/success', (req, res) => {
  setRedirectSignal(req, 'REDIRECT_SUCCESS');
  res.status(200).send(renderReturnPage({
    title: 'Payment Redirect: Success',
    message: 'Maya redirected the browser to your success URL.',
    type: 'success',
    req
  }));
});

app.get('/maya-return/failure', (req, res) => {
  setRedirectSignal(req, 'REDIRECT_FAILURE');
  res.status(200).send(renderReturnPage({
    title: 'Payment Redirect: Failure',
    message: 'Maya redirected the browser to your failure URL. This usually means the payment attempt was declined or invalid in sandbox.',
    type: 'failure',
    req
  }));
});

app.get('/maya-return/cancel', (req, res) => {
  setRedirectSignal(req, 'REDIRECT_CANCEL');
  res.status(200).send(renderReturnPage({
    title: 'Payment Redirect: Cancelled',
    message: 'Maya redirected the browser to your cancel URL.',
    type: 'cancel',
    req
  }));
});

app.post('/api/maya/create-checkout', async (req, res) => {
  try {
    const authHeader = buildBasicAuthHeader();
    const body = req.body || {};

    const response = await fetch(`${mayaBaseUrl}/checkout/v1/checkouts`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Accept': 'application/json',
        'Authorization': authHeader
      },
      body: JSON.stringify(body)
    });

    const rawText = await response.text();
    let data = {};
    try {
      data = rawText ? JSON.parse(rawText) : {};
    } catch {
      data = { raw: rawText };
    }

    if (!response.ok) {
      return res.status(response.status).json({
        message: 'Maya create checkout failed.',
        mayaStatus: response.status,
        details: data
      });
    }

    const responseCheckoutId = String(data.checkoutId || data.id || '').trim();
    const requestReferenceNumber = String(body.requestReferenceNumber || '').trim();
    if (responseCheckoutId && requestReferenceNumber) {
      requestRefToCheckout.set(requestReferenceNumber, responseCheckoutId);
    }

    return res.json({
      checkoutId: responseCheckoutId,
      redirectUrl: data.redirectUrl || data.redirectURL || '',
      status: data.status || '',
      raw: data
    });
  } catch (error) {
    return res.status(500).json({
      message: error.message || 'Unhandled backend error.'
    });
  }
});

app.get('/api/maya/checkout-status/:checkoutId', async (req, res) => {
  const checkoutId = req.params.checkoutId;
  if (!checkoutId) {
    return res.status(400).json({ message: 'checkoutId is required.' });
  }

  try {
    const authHeader = buildBasicAuthHeader();

    const response = await fetch(`${mayaBaseUrl}/checkout/v1/checkouts/${encodeURIComponent(checkoutId)}`, {
      method: 'GET',
      headers: {
        'Accept': 'application/json',
        'Authorization': authHeader
      }
    });

    const rawText = await response.text();
    let data = {};
    try {
      data = rawText ? JSON.parse(rawText) : {};
    } catch {
      data = { raw: rawText };
    }

    if (!response.ok) {
      const fallback = redirectSignals.get(checkoutId);
      if (fallback) {
        return res.json({
          checkoutId,
          status: fallback.status,
          paid: Boolean(fallback.paid),
          source: fallback.source,
          updatedAt: fallback.updatedAt,
          fallbackReason: {
            message: 'Maya status API unavailable for current key scope; using redirect fallback.',
            mayaStatus: response.status,
            details: data
          }
        });
      }

      const mayaErrorCode = String(data.code || '').toUpperCase();
      if (response.status === 401 && mayaErrorCode === 'K004') {
        return res.json({
          checkoutId,
          status: 'PENDING',
          paid: false,
          source: 'status-api-unavailable',
          fallbackReason: {
            message: 'Maya status API unavailable for current key scope and no redirect signal yet.',
            mayaStatus: response.status,
            details: data
          }
        });
      }

      return res.status(response.status).json({
        message: 'Maya checkout status lookup failed.',
        mayaStatus: response.status,
        details: data
      });
    }

    const status = data.status || data.paymentStatus || data.transactionStatus || '';

    return res.json({
      checkoutId,
      status,
      paid: isPaidStatus(status),
      raw: data
    });
  } catch (error) {
    return res.status(500).json({
      message: error.message || 'Unhandled backend error.'
    });
  }
});

app.listen(port, () => {
  console.log(`[maya-backend] listening on http://localhost:${port}`);
  console.log(`[maya-backend] using Maya base URL: ${mayaBaseUrl}`);
});
