<div align="center">

# 🧭 CouchIA: IA Mentor de Carreira

**Uma entrevista guiada por IA que descobre seu perfil, recomenda áreas de tecnologia, mapeia as habilidades e monta um plano de estudos.**

![.NET 8](https://img.shields.io/badge/.NET-8-512BD4?logo=dotnet&logoColor=white)
![React 18](https://img.shields.io/badge/React-18-61DAFB?logo=react&logoColor=black)
![TypeScript](https://img.shields.io/badge/TypeScript-3178C6?logo=typescript&logoColor=white)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-4169E1?logo=postgresql&logoColor=white)
![Testes](https://img.shields.io/badge/testes-property--based-success)
![WCAG](https://img.shields.io/badge/WCAG-2.1%20AA-blue)

</div>

---

## ✨ O que ele faz

1. **Entrevista guiada**: o mentor conversa com você, uma pergunta por vez, sobre seis categorias: interesses, experiência prévia, formação, estilo de trabalho, disponibilidade semanal e objetivos.
2. **Recomendações**: de 3 a 5 áreas de atuação, cada uma com pontuação de 0 a 100 e uma justificativa que cita itens do seu perfil. Há aviso de baixa confiança quando menos de 3 categorias foram respondidas.
3. **Feedback**: marque cada recomendação como útil ou não útil. Áreas "não úteis" descem na lista, sem alterar a pontuação.
4. **Mapa de habilidades**: todas as habilidades da área, separadas em essenciais, importantes e desejáveis. Você marca as que já domina.
5. **Plano de desenvolvimento**: etapas ordenadas por pré-requisitos, com duração calculada pela sua disponibilidade e recursos de estudo. Exporta em **PDF** ou **Markdown**.
6. **Privacidade**: sem cadastro. Os dados ficam ligados a um cookie de sessão e você pode apagar tudo com uma confirmação.

## 🏛️ Arquitetura

```
┌────────────────────┐   HTTPS + cookie    ┌─────────────────────────────────┐
│  Frontend (SPA)    │ ───────────────────▶│  WebUI (ASP.NET Core 8)         │
│  React + Vite + TS │ ◀─── SSE (chat) ─── │  controllers finos, middlewares │
└────────────────────┘                     └───────────────┬─────────────────┘
                                                           │ MediatR
                                            ┌──────────────▼────────────────┐
                                            │  Application (CQRS)           │
                                            │  casos de uso, validators,    │
                                            │  regras de entrevista e plano │
                                            └──────┬─────────────────┬──────┘
                                                   │                 │
                                  ┌────────────────▼───┐   ┌─────────▼───────────┐
                                  │ Domain             │   │ Infrastructure      │
                                  │ entidades e enums  │   │ EF Core + PostgreSQL│
                                  └────────────────────┘   │ Anthropic, QuestPDF │
                                                           │ AES-256-GCM         │
                                                           └─────────────────────┘
```

Clean Architecture com CQRS (MediatR) e FluentValidation. Cada caso de uso fica em uma pasta por funcionalidade, e a dependência sempre aponta para o centro.

| Camada | Projeto | Responsabilidade |
|---|---|---|
| Domain | `CouchIA.Domain` | Entidades, value objects e enums, sem dependência de framework |
| Application | `CouchIA.Application` | Comandos e queries, validadores, máquina de estados da entrevista, serviços de plano e mapa, prompt builder |
| Infrastructure | `CouchIA.Infrastructure` | EF Core e PostgreSQL, cliente Anthropic, criptografia, exportadores PDF e Markdown, provider do catálogo |
| WebUI | `CouchIA.WebUI` | API, SSE, cookie de sessão, ProblemDetails, cabeçalhos de segurança, rate limiting |

## 🔒 Segurança

- **Sessão** identificada por 256 bits aleatórios em cookie `HttpOnly`, `Secure` e `SameSite=Strict`. Nenhuma rota aceita o identificador de outra sessão.
- **Criptografia em repouso** (AES-256-GCM, nonce único por operação) em mensagens, perfil, justificativas e planos.
- **Prompt injection**: o prompt de sistema é fixo e o texto da pessoa vai isolado entre delimitadores.
- **Segredos** (chave da API, connection string) nunca vão a logs: passam por um redator antes.
- HTTPS com HSTS, CSP restritiva, limite de corpo de 64 KB e rate limiting por IP e por sessão.
## 🚀 Como rodar

### Pré-requisitos

- [.NET SDK 8](https://dotnet.microsoft.com/download)
- Node.js 20 ou superior
- PostgreSQL 16 (ou Docker)
- Uma chave de API da Anthropic

### 1. Banco de dados

```bash
docker run -d --name couchia-pg -p 5432:5432 \
  -e POSTGRES_PASSWORD=couchia -e POSTGRES_DB=couchia postgres:16-alpine
```

### 2. Backend

Defina os segredos por variável de ambiente (nunca no código):

```powershell
$env:ConnectionStrings__Default = "Host=localhost;Database=couchia;Username=postgres;Password=couchia"
$env:Criptografia__ChaveBase64  = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$env:Anthropic__ApiKey          = "<sua-chave>"
$env:Cors__OrigensPermitidas__0 = "http://localhost:5173"
```

```bash
cd backend
dotnet tool restore
dotnet ef database update --project src/CouchIA.Infrastructure --startup-project src/CouchIA.WebUI
dotnet run --project src/CouchIA.WebUI --launch-profile https
```

A API sobe em `https://localhost:7262` e o Swagger fica em `/swagger` (ambiente Development).
O cookie de sessão é `Secure`, então use HTTPS. Rode `dotnet dev-certs https --trust` uma vez, se precisar.

> A chave de criptografia precisa ser a mesma entre execuções. Se você trocar a chave, os dados já gravados deixam de poder ser lidos.

### 3. Frontend

```bash
cd frontend
npm install
export VITE_API_TARGET=https://localhost:7262   # proxy de /api no ambiente de desenvolvimento
npm run dev
```

No PowerShell, use `$env:VITE_API_TARGET = "https://localhost:7262"`. Abra `http://localhost:5173`.

## 🐳 Rodar com Docker (demo com mock)

Sobe banco, API e frontend com um **LLM falso e determinístico**, sem chave da Anthropic. Serve para ver o app funcionando, não para uso real.

```bash
docker compose up -d --build
```

Abra **http://localhost:8080**. Para parar: `docker compose down` (acrescente `-v` para apagar o banco).

- O mock conduz as seis perguntas em ordem, lê as horas semanais do seu texto e recomenda áreas por palavras-chave (backend, frontend, dados). Selecionado por `Llm__Provedor=Mock`; o padrão continua sendo a Anthropic.
- A API aplica as migrações ao subir (`Banco__AplicarMigracoes=true`), e o nginx do frontend faz proxy de `/api` no mesmo origin.
- O nginx informa `X-Forwarded-Proto: https` e a API só confia nesse cabeçalho de redes privadas (`Proxy__ConfiarEmEncaminhamento=true`), para emitir o cookie `Secure` mesmo em `http://localhost`. Fora desse modo, o redirecionamento e o HSTS não mudam.
- A senha do banco e a chave de criptografia do `docker-compose.yml` são **somente para demonstração**.
- **Safari** não aceita cookie `Secure` em `http://localhost`, então a sessão não persiste nele. Use Chrome ou Firefox.
- Capturas de tela: `cd frontend && node scripts/capturar-telas.mjs` (grava em `docs/screenshots/`).

## 🧪 Testes

```bash
# Backend (os testes de integração usam Testcontainers, então precisam do Docker)
cd backend && dotnet test

# Frontend
cd frontend
npm run typecheck
npm test           # Vitest + Testing Library + fast-check
npm run e2e    # Playwright + axe-core (WCAG 2.1 A/AA, 320 a 1920 px)
```

A suíte combina três estilos:

- **Property-based** (FsCheck e fast-check) para as regras de negócio: validação de mensagem, máquina de entrevista, recomendações, plano, catálogo e criptografia.
- **Integração** com PostgreSQL real via Testcontainers e um LLM falso determinístico.
- **E2E de acessibilidade**: sem rolagem horizontal, navegação completa por teclado e zero violações do axe.

## 📚 Catálogo de carreiras

As áreas, habilidades, pré-requisitos e recursos de estudo vêm de um arquivo versionado:
[`backend/src/CouchIA.WebUI/Catalogo/catalogo.json`](backend/src/CouchIA.WebUI/Catalogo/catalogo.json).

O catálogo inicial tem 3 áreas (Backend, Frontend e Ciência de Dados) e 17 habilidades. Ele é validado ao iniciar: limites de tamanho, identificadores únicos, pré-requisitos existentes e sem ciclos. Se uma carga for inválida, o catálogo ativo anterior é mantido e a falha é registrada em log.

## 🗂️ Estrutura do repositório

```
CouchIA/
├── backend/
│   ├── src/        Domain · Application · Infrastructure · WebUI
│   └── tests/      xUnit + FsCheck + Testcontainers
├── frontend/       SPA React + Vite + TypeScript, testes Vitest e Playwright
└── .kiro/specs/    requisitos, design e tarefas do projeto
```

## 📄 Especificação

O projeto foi construído a partir de uma spec em [`.kiro/specs/ia-mentor-carreira`](.kiro/specs/ia-mentor-carreira):
[requisitos](.kiro/specs/ia-mentor-carreira/requirements.md) ·
[design](.kiro/specs/ia-mentor-carreira/design.md) ·
[tarefas](.kiro/specs/ia-mentor-carreira/tasks.md).

## ⚠️ Aviso

As recomendações, o mapa e o plano são gerados por IA e servem como orientação inicial. Não substituem uma conversa com um profissional de carreira.