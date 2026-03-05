const express = require('express');
const cors = require('cors');
const dotenv = require('dotenv');

dotenv.config();

const app = express();
app.use(cors());
app.use(express.json());

const port = Number(process.env.PORT || 8787);
const mayaBaseUrl = (process.env.MAYA_BASE_URL || 'https://pg-sandbox.paymaya.com').replace(/\/$/, '');

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
    'COMPLETED',
    'APPROVED',
    'CAPTURED'
  ].includes(value);
}

app.get('/health', (_req, res) => {
  res.json({ ok: true, mayaBaseUrl });
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

    return res.json({
      checkoutId: data.checkoutId || data.id || '',
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
