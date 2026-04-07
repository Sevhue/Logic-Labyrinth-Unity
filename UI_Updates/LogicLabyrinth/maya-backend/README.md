# Maya Checkout Backend (Sandbox)

This backend proxies Unity requests to Maya Checkout so API keys stay off the game client.

## 1) Configure

Copy `.env.example` to `.env` and set at least one credential option:

- `MAYA_BASIC_AUTH=Basic ...`
- or `MAYA_PUBLIC_KEY` (+ optional `MAYA_SECRET_KEY`)

## 2) Run

```bash
cd maya-backend
npm install
npm start
```

Server runs by default at `http://localhost:8787`.

## Endpoints

- `POST /api/maya/create-checkout`
- `GET /api/maya/checkout-status/:checkoutId`
- `GET /health`

## Unity side

Set `PauseMenuController.mayaBackendBaseUrl` to your backend URL.

Important: In production, host this backend on a secure server and configure proper redirect URLs/webhooks.
