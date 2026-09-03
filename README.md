# Kynakee

> AI-Powered Construction Intelligence Platform  
> From Site Visit to Professional Offer in Minutes

Kynakee is an AI-native platform designed to transform construction site observations, photos, videos, plans, measurements, and conversations into a complete and traceable commercial proposal.

The platform combines AI agents, a global construction knowledge base, MCP provider networks, planning engines, project valuation, and conversational interfaces to help construction professionals generate high-quality budgets significantly faster than traditional methods. 【1-d473ea】【1-393aad】

---

# Vision

Kynakee exists to become the operating system of the construction industry.

The first product focus is intelligent budget generation for:

- Independent contractors
- Renovation professionals
- Small and medium construction companies

Future evolution includes:

- Procurement
- Contracting
- Execution control
- Certifications
- Cost control
- Productivity analysis
- Construction operations management

Kynakee begins as a budgeting platform and evolves into a complete Construction Operating System. 【1-d473ea】

---

# Core Value Proposition

A construction professional can:

```text
Site Visit
    ↓
Capture Evidence
    ↓
AI Understanding
    ↓
Scope Definition
    ↓
APU Generation
    ↓
Planning
    ↓
Valuation
    ↓
Review
    ↓
Commercial Offer
```

All from:

- WhatsApp
- Telegram
- Web Application

using a single operational workflow. 【1-d473ea】

---

# Key Principles

## Conversational by Design

Every artifact can be:

- Created
- Queried
- Modified
- Explained
- Audited

through natural language conversation. 【1-d473ea】

---

## Multichannel Experience

Supported channels:

- WhatsApp
- Telegram
- Web Application

The same project can be managed through any channel. 【1-d473ea】【1-393aad】

---

## Planning as a First-Class Citizen

Scheduling is a core capability.

Kynakee generates:

- Activity sequencing
- Dependencies
- Critical path
- PERT diagrams
- Gantt diagrams
- Project duration forecasts

as part of the budgeting workflow. 【1-d473ea】

---

## Global Multilingual Knowledge Base

Knowledge is language-independent.

Every construction concept is represented by a Canonical Concept.

Example:

```text
PART_TILE_WALL_PORCELAIN
```

Translations:

```text
ES → Alicatado porcelánico pared
EN → Porcelain wall tiling
FR → Carrelage mural en porcelaine
```

This allows knowledge reuse across countries, regions, and languages. 【1-d473ea】【1-13d16a】

---

# Operational Workflow

Kynakee implements a 9-phase operational model:

| Phase | Name |
|---------|---------|
| 0 | Initialization |
| 1 | Capture |
| 2 | Context |
| 3 | Scope |
| 4 | Production |
| 5 | Planning |
| 6 | Valuation |
| 7 | Review |
| 8 | Offer |

Each phase generates domain artifacts that become inputs for the next phase. 【1-d473ea】【1-13d16a】

---

# Platform Architecture

Kynakee is built as a:

```text
Modular Monolith
+
Domain Driven Design (DDD)
+
CQRS
+
Vertical Slice Architecture
+
Event Driven Architecture
```

The platform is organized into seven bounded contexts. 【1-393aad】【1-13d16a】【1-534902】

## Modules

```text
Projects
KnowledgeBase
MCP
AI
Bots
Billing
Identity
```

Projects is the Core Domain.

All business value centers around the Project aggregate and its lifecycle. 【1-13d16a】【1-534902】

---

# Technology Stack

## Backend

```text
.NET 10
ASP.NET Core
MediatR
FluentValidation
MassTransit
SignalR
Hangfire
Serilog
OpenTelemetry
YARP
Polly
```

【1-393aad】【1-534902】

## Frontend

```text
Next.js 15
TypeScript
Tailwind CSS
shadcn/ui
TanStack Query
Zustand
React Hook Form
Zod
```

【1-393aad】【1-534902】

## Infrastructure

```text
PostgreSQL 17
Qdrant
RabbitMQ
Redis
Docker
Traefik
Hetzner Cloud
Cloudflare
```

【1-393aad】【1-baf23c】【1-534902】

---

# AI Architecture

Kynakee uses specialized agents through Microsoft Agents Framework. 【1-393aad】【1-534902】

## Agents

```text
CaptureAgent
ScopeAgent
ProductionAgent
PlanningAgent
ValuationAgent
OfferAgent
ConversationAgent
EmbeddingService
```

Each agent serves a specific phase of the workflow. 【1-393aad】【1-534902】

---

# Knowledge Base

The Knowledge Base stores:

```text
Canonical Concepts
APU Templates
Translations
Production Yields
Embeddings
Historical Knowledge
```

APU templates contain:

```text
Materials
Labor
Equipment
Subcontractors
Transport
Yields
```

but do not contain prices.

Prices are determined dynamically during project valuation. 【1-13d16a】【1-d473ea】【1-baf23c】

---

# MCP Provider Network

Kynakee integrates a distributed MCP ecosystem.

Providers can expose:

- Materials
- Labor rates
- Equipment
- Transport
- Specialized subcontracting services

through MCP endpoints.

Valuation uses:

```text
MCP Provider
    ↓
Cache
    ↓
Internet
```

as a fallback chain. 【1-d473ea】【1-393aad】【1-534902】

---

# Multi-Tenancy

Kynakee is multi-tenant by design.

Hierarchy:

```text
Platform
    ↓
Tenant
    ↓
Workspace
    ↓
User
    ↓
Project
```

Data isolation is enforced by TenantId and Row-Level Security principles. 【1-d473ea】【1-393aad】【1-534902】

---

# AI Act Compliance

Kynakee is designed to comply with:

```text
EU AI Act
GDPR
OWASP
PSD2
```

Key requirements implemented:

- Human oversight
- Explainability
- Auditability
- AI decision logging
- Transparency disclosures

Review (Phase 7) is mandatory before offer generation. 【1-d473ea】【1-393aad】【1-13d16a】

---

# Development Principles

Kynakee follows:

```text
SOLID
DDD Tactical Patterns
CQRS
Event Driven Design
OpenAPI First
Test First
Copilot Assisted Development
```

Non-negotiable architecture rules are defined in the Copilot Instructions and ADR catalog. 【1-b28b4f】【1-534902】

---

# Repository Structure

This repository contains the backend platform and infrastructure services, including the independent `Kynakee.Gateway` project. The frontend lives in a separate repository named `kynakee-web` and is deployed as an independent Docker container. Each repository owns its Docker Compose configuration.

```text
kynakee-platform/                  # Backend repository
├── src/
│   ├── Kynakee.Gateway            # Public YARP gateway
│   ├── Kynakee.Api                # Internal API host
│   └── Kynakee.Modules
│       ├── Projects
│       ├── KnowledgeBase
│       ├── MCP
│       ├── AI
│       ├── Bots
│       ├── Billing
│       └── Identity
├── tests/
│   ├── Kynakee.UnitTests
│   ├── Kynakee.IntegrationTests
│   ├── Kynakee.ContractTests
│   └── Kynakee.E2ETests          # Backend/system E2E and API lifecycle validation
├── docker/
│   ├── docker-compose.dev.yml
│   ├── docker-compose.staged.yml
│   └── docker-compose.prod.yml
├── README.md
└── Kynakee.slnx

kynakee-web/                      # Frontend repository (separate)
├── app/
├── components/
├── lib/
├── public/
├── e2e/                          # Browser E2E tests for the UI
├── package.json
├── Dockerfile
└── docker-compose.yml
```

## Frontend and Backend Separation

The platform is split between two repositories and independently deployed application services:

- `kynakee-platform`: .NET 10 backend, API gateway, business logic, background jobs, and shared infrastructure.
- `kynakee-web`: Next.js 15 frontend for the user experience.

Both repositories are deployed independently, but the web application interacts with the platform through the gateway (`REST` and `SignalR`). The backend Compose starts `Kynakee.Gateway` and `Kynakee.Api` together, sharing platform services (PostgreSQL, Redis, RabbitMQ, Qdrant, etc.). The gateway is the published HTTP entry point and the API is internal to the Compose network. The current gateway project is scaffolded; YARP routing is implemented in US-010. This repository does not build or start `kynakee-web`; the frontend repository must be started with its own Compose configuration.

E2E validation lives in both repositories:

- `kynakee-platform`: backend/system E2E tests for API flows, lifecycle validation, and platform integration.
- `kynakee-web`: browser E2E tests for the end-user workflow and UI journeys.

This separation keeps the backend domain and the UI lifecycle independent, improves deployability, and simplifies team ownership.

## Deployment topology and environment policy

The backend repository maintains Compose files for development, production, and a retained staged configuration. Only development and production are currently operational:

| Environment | Branch | Compose file | Public HTTP entry point |
|---|---|---|---|
| Development | `develop` | `docker/docker-compose.dev.yml` | Gateway |
| Staged (not deployed) | `staged` | `docker/docker-compose.staged.yml` | Gateway |
| Production | `master` | `docker/docker-compose.prod.yml` | Gateway via Traefik |

The gateway is the only published application endpoint. `Kynakee.Api` and the infrastructure services remain internal to the Docker network. Environment files are templates only; copy the appropriate `.env.*.example`, replace every `CHANGE_ME` value locally, and do not commit the resulting file.

The complete record of these decisions is maintained in [`docs/ADR/kynakee-adrs-part3.md`](docs/ADR/kynakee-adrs-part3.md), and the Hetzner procedure is maintained in [`docs/Deployment/US-005-Hetzner-Runbook.md`](docs/Deployment/US-005-Hetzner-Runbook.md).

【1-5f8011】【1-b28b4f】【1-534902】

---

# Sprint 0 Objective

The first milestone establishes the complete technical foundation:

- Infrastructure
- Authentication
- Project Aggregate
- Billing Foundation
- Outbox Pattern
- Observability
- Frontend Foundation
- Bot Foundation

Success criteria:

```text
Login
    ↓
Create Project
    ↓
Persist
    ↓
Retrieve
    ↓
Observe Complete Traceability
```

【1-5f8011】

---

# Status

```text
Version: MVP
Architecture: Approved
Domain Model: Approved
Database Design: Approved
API Contracts: Approved
ADRs: Approved
Sprint 0: Defined
```

【1-13d16a】【1-d473ea】【1-393aad】【1-baf23c】【1-de04dc】【1-5f8011】【1-534902】

---

# License

Copyright © Kynakee.

All rights reserved.

Confidential and proprietary.
