# WhatsApp Multi-Tenant Gateway

Gateway independente em .NET 8 para centralizar a comunicação com a WhatsApp Business Cloud API.
Ele aceita vários tenants e vários números por tenant sem acessar banco, agenda ou regras de negócio do sistema principal.

## Responsabilidades

- cadastrar tenants e suas URLs de callback;
- cadastrar Meta Apps e vários canais/números por tenant;
- validar o `hub.verify_token` e a assinatura `X-Hub-Signature-256` da Meta;
- resolver automaticamente o tenant e o canal pelo endereço exclusivo do webhook;
- deduplicar mensagens e atualizações de status;
- encaminhar eventos recebidos ao sistema do tenant;
- enviar texto, templates e templates com PDF dinâmico;
- fazer upload privado de mídia para a Meta;
- criptografar Access Token, App Secret, Verify Token e segredo do callback no PostgreSQL.

## Fluxo

```text
Meta -> /api/webhooks/whatsapp/{webhookKey-do-meta-app}
     -> valida assinatura com o App Secret do Meta App
     -> resolve phone_number_id -> canal -> tenant
     -> registra/deduplica evento
     -> identifica tenant + canal
     -> POST no callbackUrl do tenant
     -> sistema principal / agenda / IA

Sistema principal -> /api/messages/* -> Meta Graph API -> cliente
```

## Configuração local

Pré-requisitos: .NET 8 e PostgreSQL. O `docker-compose.yml` é opcional para quem possui Docker.

Gere os segredos no PowerShell, inclusive em versões antigas:

```powershell
$bytes = New-Object byte[] 32
$rng = [Security.Cryptography.RandomNumberGenerator]::Create()
$rng.GetBytes($bytes)
[Convert]::ToBase64String($bytes)
$rng.Dispose()
```

Configure as variáveis:

```powershell
$env:DATABASE_URL = "Host=localhost;Port=5433;Database=whatsapp_gateway;Username=postgres;Password=postgres"
$env:Gateway__AdminApiKey = "um-segredo-interno-longo"
$env:Gateway__EncryptionKey = "BASE64_GERADO_ACIMA"
$env:Gateway__PublicBaseUrl = "https://seu-gateway.up.railway.app"
$env:Gateway__DefaultGraphApiVersion = "v23.0"
dotnet run
```

O schema mínimo é criado automaticamente no primeiro início. Em produção, mantenha a mesma
`Gateway__EncryptionKey`: trocá-la sem recriptografar os dados torna as credenciais existentes ilegíveis.

## 1. Cadastrar um Meta App

O callback da Meta pertence ao aplicativo, e não a um número específico. Um Meta App pode ter
vários números, inclusive de tenants diferentes quando a operação SaaS utilizar Embedded Signup.

```http
POST /api/admin/apps
X-Api-Key: um-segredo-interno-longo
Content-Type: application/json

{
  "name": "App Meta principal",
  "metaAppId": "123456789",
  "appSecret": "APP_SECRET_DA_META",
  "verifyToken": "TOKEN_ESCOLHIDO_POR_VOCE"
}
```

A resposta contém a `webhookUrl`. Cadastre essa URL e o mesmo `verifyToken` no painel da Meta,
depois assine o campo `messages`. Apps Meta diferentes recebem URLs diferentes.

## 2. Cadastrar uma barbearia

Todas as rotas `/api/admin/*` e `/api/messages/*` exigem `X-Api-Key`.

```http
POST /api/admin/tenants
X-Api-Key: um-segredo-interno-longo
Content-Type: application/json

{
  "name": "Barbearia Centro",
  "callbackUrl": "https://barber-io.example.com/api/integrations/whatsapp/events",
  "callbackSecret": "segredo-compartilhado-com-o-barber-io"
}
```

O callback é opcional. Quando configurado, o gateway assina o corpo com
`X-Gateway-Signature-256: sha256=...`.

## 3. Cadastrar um número

```http
POST /api/admin/tenants/{tenantId}/channels
X-Api-Key: um-segredo-interno-longo
Content-Type: application/json

{
  "appId": "APP_INTERNO_CRIADO_NO_PASSO_1",
  "name": "Unidade Centro",
  "wabaId": "123456789",
  "phoneNumberId": "987654321",
  "displayPhoneNumber": "+55 11 99999-9999",
  "accessToken": "TOKEN_DA_META",
  "graphApiVersion": "v23.0"
}
```

Cada novo número aponta para o Meta App correspondente e pode pertencer ao mesmo tenant ou a outro.
O `phoneNumberId` é globalmente único e faz o roteamento do evento para a barbearia correta.

## 4. Evento entregue ao sistema principal

```json
{
  "tenantId": "a89dc229-0b1b-4c87-a510-65b446608111",
  "channelId": "49404987-b879-4716-a76e-ac04aeac4c86",
  "phoneNumberId": "987654321",
  "eventId": "wamid.HBgN...",
  "eventType": "message",
  "from": "5511999999999",
  "customerName": "Maria",
  "messageType": "text",
  "text": "Tem horário amanhã?",
  "receivedAt": "2026-09-15T15:00:00Z"
}
```

## 5. Enviar texto dentro da janela de atendimento

```http
POST /api/messages/text
X-Api-Key: um-segredo-interno-longo
Content-Type: application/json

{
  "tenantId": "TENANT_ID",
  "channelId": "CHANNEL_ID",
  "to": "5511999999999",
  "text": "Tenho 10:00 com João e 10:30 com Carlos."
}
```

## 6. Primeira mensagem com PDF diferente por usuário

Crie e aprove na Meta um único template com cabeçalho `DOCUMENT`, por exemplo
`envio_documento`, e corpo `Olá, {{1}}. Segue o documento {{2}}.`.

```http
POST /api/messages/template-document
X-Api-Key: um-segredo-interno-longo
Content-Type: application/json

{
  "tenantId": "TENANT_ID",
  "channelId": "CHANNEL_ID",
  "to": "5511999999999",
  "templateName": "envio_documento",
  "languageCode": "pt_BR",
  "documentUrl": "https://arquivos.example.com/documentos/cliente-123.pdf",
  "mediaId": null,
  "filename": "Documento-Maria.pdf",
  "bodyParameters": [
    { "type": "text", "text": "Maria" },
    { "type": "text", "text": "do agendamento 4587" }
  ]
}
```

Informe exatamente um entre `documentUrl` e `mediaId`. Para um arquivo sensível, envie primeiro
como `multipart/form-data` em `POST /api/messages/media`, receba o `id` da Meta e use-o em `mediaId`.

## Deploy no Railway

Crie um serviço PostgreSQL e um serviço apontando para esta pasta/repositório. Configure:

- `DATABASE_URL` (normalmente fornecida pelo PostgreSQL do Railway);
- `Gateway__AdminApiKey`;
- `Gateway__EncryptionKey`;
- `Gateway__PublicBaseUrl` com a URL pública do serviço;
- `Gateway__DefaultGraphApiVersion=v23.0`.

O `Dockerfile`, `railway.json` e `/healthz` já estão preparados para o deploy.

## Limites desta base

O gateway não interpreta intenção e não consulta agenda. Essas responsabilidades permanecem no
sistema de cada tenant, que recebe a mensagem pelo callback e responde chamando `/api/messages/*`.
O callback é tentado uma vez nesta versão inicial; eventos com falha ficam registrados como `failed`
para permitir a adição posterior de um worker de retry sem alterar o contrato da API.
