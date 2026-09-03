# KYNAKEE PLATFORM
## Technical Roadmap
### 18-Month Plan · MVP to Scale · .NET 10 · Next.js 15 · Hetzner
**Version 1.0 · 2026-08-20 · Confidential**

---

## Table of Contents

- [1. Roadmap Overview](#s1)
- [2. Phase 0 — Sprint 0: Technical Foundation (Month 1)](#s2)
- [3. Phase 1 — Sprint 1: Core Phase Operations (Month 2)](#s3)
- [4. Phase 2 — Sprint 2: AI Integration & MCP Network (Month 3)](#s4)
- [5. Phase 3 — Sprint 3: Bot Channels & Real-Time (Month 4)](#s5)
- [6. Phase 4 — Sprint 4: Billing, Payments & Plans (Month 5)](#s6)
- [7. Phase 5 — Sprint 5: Knowledge Base & APU Library (Month 6)](#s7)
- [8. Phase 6 — Months 7-9: Hardening & Scale](#s8)
- [9. Phase 7 — Months 10-12: LATAM Expansion](#s9)
- [10. Phase 8 — Months 13-18: Enterprise & Microservices Migration](#s10)
- [11. Technical Debt & Non-Functional Roadmap](#s11)
- [12. Key Milestones Summary](#s12)

---

## 1. Roadmap Overview {#s1}

This Technical Roadmap defines the 18-month development plan for the Kynakee platform, from MVP launch to enterprise-grade scale. It is organized in 8 phases aligned with business milestones and technical maturity.

| Phase | Timeline | Focus | Key Deliverable |
|---|---|---|---|
| Phase 0 | Month 1 | Technical Foundation | MVP: POST /api/v1/projects working end-to-end |
| Phase 1 | Month 2 | Core Phase Operations | All 9 phases functional (no AI yet) |
| Phase 2 | Month 3 | AI + MCP Integration | Full AI-powered budget generation |
| Phase 3 | Month 4 | Bot Channels | Telegram + WhatsApp fully operational |
| Phase 4 | Month 5 | Billing + Payments | Stripe subscriptions + credit system live |
| Phase 5 | Month 6 | Knowledge Base | APU library with 500+ templates |
| Phase 6 | Months 7-9 | Hardening | 99.5% uptime, 80% test coverage, security audit |
| Phase 7 | Months 10-12 | LATAM Expansion | Mexico/Colombia market, multi-region |
| Phase 8 | Months 13-18 | Enterprise | BIM integration, microservices extraction |

> **North Star Metric:** Number of APUs in the Global Knowledge Base. This metric captures both user adoption (more projects = more APUs) and product quality (more APUs = better accuracy = lower cost per project).

---

## 2. Phase 0 — Sprint 0: Technical Foundation (Month 1) {#s2}

**Goal:** Establish the complete technical foundation. No business features. Only infrastructure, scaffolding, and the first working vertical slice.

> **Definition of Done:** `docker-compose up` → `POST /api/v1/projects` returns 201 with JWT auth, tenant isolation, EF Core migrations applied, and response logged with CorrelationId.

### Key Deliverables

- GitHub repository with Gitflow + CI pipeline (80% coverage gate)
- `docker-compose.dev.yml` with all 7 services (PostgreSQL, Qdrant, Redis, RabbitMQ, NGrok)
- .NET 10 solution with all 7 module projects + shared kernel
- 6 MediatR pipeline behaviors in correct order
- YARP gateway with JWT + CorrelationId + rate limiting
- Identity module: register + login endpoints
- Projects module: Project aggregate (20+ unit tests) + CreateProject vertical slice
- Billing module: CreditAccount aggregate + TenantCreated consumer
- MassTransit + Outbox + RabbitMQ operational
- Next.js 15: login + dashboard + project list
- Telegram bot: basic conversation flow

### Technical Risks

| Risk | Probability | Mitigation |
|---|---|---|
| 1-month timeline too aggressive | Medium | Focus on P0 deliverables only. P1/P2 move to Sprint 1. |
| MassTransit Outbox configuration complexity | Low | Use MassTransit official PostgreSQL Outbox docs. Test early. |
| Next.js + .NET CORS issues | Low | Configure CORS in YARP from day 1. |

---

## 3. Phase 1 — Sprint 1: Core Phase Operations (Month 2) {#s3}

**Goal:** All 9 project phases functional with manual data entry (no AI yet). Users can create a complete project and generate an offer using manually entered data.

### Key Deliverables

- Phase 1 (Capture): multimedia upload endpoint + file storage (S3-compatible)
- Phase 2 (Context): territorial + normative + labor + economic data entry
- Phase 3 (Scope): manual work item creation + canonical concept lookup
- Phase 4 (Production): manual APU assignment from KnowledgeBase
- Phase 5 (Planning): manual schedule creation with PERT/Gantt data
- Phase 6 (Valuation): manual component pricing entry
- Phase 7 (Review): human approval flow + AI Act Art. 14 compliance log
- Phase 8 (Offer): PDF generation (wkhtmltopdf or similar)
- SignalR real-time notifications for phase transitions
- Next.js: full project lifecycle UI (all 9 phases)

### Success Metrics

| Metric | Target |
|---|---|
| End-to-end project completion (manual) | < 30 minutes |
| API response time (non-AI endpoints) | < 200ms p95 |
| Test coverage | > 80% |
| First paying customer | 1 reformador using the system |

---

## 4. Phase 2 — Sprint 2: AI Integration & MCP Network (Month 3) {#s4}

**Goal:** Full AI-powered budget generation. Users can complete a project from site visit to offer in under 30 minutes using AI assistance.

### Key Deliverables

- Microsoft Agents Framework integration (DeepSeek-V3 + Gemini Flash)
- CaptureAgent: image/video analysis + work item extraction
- ScopeAgent: canonical concept mapping from capture evidence
- ProductionAgent: APU generation from KnowledgeBase or new
- PlanningAgent: automatic schedule sequencing + critical path
- ValuationAgent: component pricing orchestration
- OfferAgent: commercial narrative generation
- TokenGateBehavior: credit reservation/consumption/release
- First 3 MCP providers integrated (ceramics, plumbing, electrical)
- Fallback chain per component (MCP → Cache → Internet)
- Gemini text-embedding-004 + Qdrant for APU semantic search

### AI Provider Configuration

| Agent | Primary Model | Fallback |
|---|---|---|
| CaptureAgent | gemini-flash | deepseek-v3 |
| ScopeAgent | deepseek-v3 | deepseek-v3 |
| ProductionAgent | deepseek-v3 | deepseek-v3 |
| PlanningAgent | deepseek-v3 | deepseek-v3 |
| ValuationAgent | deepseek-v3 | deepseek-v3 |
| OfferAgent | gemini-flash | deepseek-v3 |
| ConversationAgent | gemma4 | deepseek-v3 |

---

## 5. Phase 3 — Sprint 3: Bot Channels & Real-Time (Month 4) {#s5}

**Goal:** Full conversational experience via Telegram and WhatsApp. Users can complete the entire 9-phase workflow from their mobile messaging app.

### Key Deliverables

- Telegram bot: full 9-phase conversational flow
- WhatsApp bot: full 9-phase conversational flow (Meta Cloud API)
- SPA (System of Project Active): multi-project management per conversation
- ConversationAgent: context-aware responses with project state
- Bot timeout/pause/resume logic (30 min timeout, 24h resume prompt)
- Motor de preguntas contextuales: ¿Por qué este rendimiento? ¿Cuánto cuesta?
- Offer PDF delivery via bot (send PDF to WhatsApp/Telegram)
- NGrok → production webhook migration (Hetzner production)

---

## 6. Phase 4 — Sprint 4: Billing, Payments & Plans (Month 5) {#s6}

**Goal:** Full credit economy operational. Stripe subscriptions live. First revenue.

### Key Deliverables

- Stripe subscription integration (Starter, Pro, Studio, Business plans)
- Bizum payment method via Stripe (Spain market)
- Credit recharge flow (extra credit packs)
- Subscription renewal automation (Hangfire recurring job)
- Credit depletion alerts via bot + email
- Admin panel: plan configuration, credit pricing, token limits
- MCP provider compensation calculation (fraction per token)
- Billing dashboard in Next.js (credit history, invoices)

### Revenue Targets

| Month | Paying Customers | MRR Target |
|---|---|---|
| Month 5 (launch) | 20 | €1.500 |
| Month 6 | 80 | €5.500 |
| Month 9 | 180 | €12.000 |
| Month 12 | 320 | €22.000 |

---

## 7. Phase 5 — Sprint 5: Knowledge Base & APU Library (Month 6) {#s7}

**Goal:** Global APU Knowledge Base with 500+ validated templates. The flywheel starts: more projects = more APUs = lower cost per project.

### Key Deliverables

- APU template import from public sources (CYPE, BEDEC, PREOC)
- Canonical concept taxonomy: 200+ concepts across 15 categories
- Multilingual support: ES, EN, FR, PT for all canonical concepts
- APU quality scoring: confidence, usage count, regional coverage
- Semantic search optimization: Qdrant HNSW index tuning
- APU contribution flow: projects auto-contribute anonymized APUs
- Admin panel: APU review, validation, canonical concept management
- KnowledgeBase API: public search endpoint for MCP providers

---

## 8. Phase 6 — Months 7-9: Hardening & Scale {#s8}

**Goal:** Production-grade reliability. 99.5% uptime. Security audit passed. Ready for aggressive growth.

### Technical Hardening

- Penetration testing by external firm
- GDPR compliance audit + DPA with all sub-processors
- AI Act Art. 4 training program documented
- AI Act System Card published
- PostgreSQL read replicas for reporting queries
- Redis cluster mode for high availability
- RabbitMQ mirrored queues
- Automated backup testing (restore drill monthly)
- Load testing: 50 concurrent users sustained

### Performance Targets

| Metric | Target |
|---|---|
| API response time (non-AI) | < 200ms p95 |
| AI operation completion | < 30s p95 |
| System uptime | > 99.5% |
| Test coverage | > 80% (line + branch + method) |
| MTTR (Mean Time to Recovery) | < 30 minutes |

---

## 9. Phase 7 — Months 10-12: LATAM Expansion {#s9}

**Goal:** First LATAM market live (Mexico or Colombia). Multi-region infrastructure operational.

### Key Deliverables

- Hetzner Ashburn (US East) deployment for LATAM
- PostgreSQL logical replication: EU → LATAM read replica
- Cloudflare geo-routing: users routed to nearest region
- Mexico: LFPDPPP compliance (data protection law)
- Colombia: Ley 1581/2012 compliance
- MXN and COP currency support in Stripe
- Mexican construction conventions (NMX, CONUEE)
- Colombian collective agreements (Convenio Construccion Colombia)
- Spanish + Portuguese language support in all bot channels
- LATAM MCP provider network: first 5 providers in Mexico City

### LATAM Revenue Targets

| Month | Total Customers | MRR Target |
|---|---|---|
| Month 12 (LATAM launch) | 320 | €22.000 |
| Month 15 | 500 | €35.000 |
| Month 18 | 750 | €55.000 |

---

## 10. Phase 8 — Months 13-18: Enterprise & Microservices Migration {#s10}

**Goal:** Enterprise features for constructoras medianas and promotoras. Begin selective microservices extraction for modules that need independent scaling.

### Enterprise Features

- BIM integration: IFC file import → automatic work item extraction
- ERP integration: SAP, Holded, Sage via MCP Server protocol
- Multi-workspace: delegaciones regionales within a tenant
- Advanced analytics: project profitability, APU accuracy trends
- White-label option for large constructoras
- API access tier: Business plan gets direct API access

### Microservices Extraction Candidates

Extract modules to independent services only when justified by scale. Each module was designed for extraction from day 1.

| Module | Extraction Trigger | Rationale |
|---|---|---|
| AI Module | > 1000 concurrent AI requests/day | Independent scaling of GPU/LLM resources |
| KnowledgeBase | > 10M APU templates in Qdrant | Dedicated vector DB cluster |
| MCP Module | > 100 provider integrations | Independent provider network management |
| Bots Module | > 50K active bot conversations | Independent bot infrastructure |
| Billing Module | Stripe volume > €100K MRR | PCI compliance isolation |

---

## 11. Technical Debt & Non-Functional Roadmap {#s11}

### Planned Technical Debt Items

| Item | Priority | Target Phase |
|---|---|---|
| Replace wkhtmltopdf with Puppeteer for PDF generation | Medium | Phase 2 |
| Add OpenTelemetry traces to AI agent calls | High | Phase 2 |
| Implement cursor-based pagination everywhere | Medium | Phase 3 |
| Add contract tests (PactNet) for all integration events | High | Phase 3 |
| Implement E2E tests with Playwright | Medium | Phase 4 |
| Add Redis cache for frequently accessed APU templates | Medium | Phase 5 |
| Implement database connection pooling (PgBouncer) | Low | Phase 6 |
| Add OpenAPI spec validation in CI pipeline | Medium | Phase 6 |

### Security Roadmap

- Month 1: JWT + HTTPS + input validation (baseline)
- Month 3: Rate limiting per tenant + OWASP Top 10 review
- Month 5: Stripe PCI compliance + secrets vault (HashiCorp Vault)
- Month 7: External penetration test
- Month 9: ISO 27001 gap analysis
- Month 12: GDPR audit + AI Act System Card

---

## 12. Key Milestones Summary {#s12}

| Month | Milestone | KPI | Status |
|---|---|---|---|
| 1 | MVP: POST /api/v1/projects working | First vertical slice | Sprint 0 |
| 2 | All 9 phases functional (manual) | First complete project | Sprint 1 |
| 3 | AI-powered budget generation | < 30 min per project | Sprint 2 |
| 4 | Telegram + WhatsApp operational | First bot project | Sprint 3 |
| 5 | First revenue | 20 paying customers, €1.5K MRR | Sprint 4 |
| 6 | Knowledge Base live | 500+ APU templates | Sprint 5 |
| 9 | Production hardening | 99.5% uptime, 180 customers | Phase 6 |
| 12 | LATAM launch | 320 customers, €22K MRR | Phase 7 |
| 18 | Enterprise + microservices | 750 customers, €55K MRR | Phase 8 |

> **Flywheel:** More reformadores → More projects → More APUs in Knowledge Base → Lower cost per project → More competitive pricing → More reformadores. This is the core growth engine of Kynakee.

---

*KYNAKEE PLATFORM · Technical Roadmap v1.0 · 2026-08-20*  
*18-Month Plan · MVP to Enterprise · Confidential*
