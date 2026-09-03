# KYNAKEE PLATFORM
## Product Requirements Document (PRD)
### AI-Powered Construction Budget Generation Platform
**Version 1.0 · 2026-08-20 · Confidential**

---

## Table of Contents

- [1. Executive Summary](#s1)
- [2. Problem Statement](#s2)
- [3. Product Vision](#s3)
- [4. Target Users & Personas](#s4)
- [5. Market Context](#s5)
- [6. Core Features & Functional Requirements](#s6)
- [7. The 9-Phase Operational Flow](#s7)
- [8. Non-Functional Requirements](#s8)
- [9. User Stories by Persona](#s9)
- [10. Acceptance Criteria](#s10)
- [11. Out of Scope (MVP)](#s11)
- [12. Success Metrics & KPIs](#s12)

---

## 1. Executive Summary {#s1}

Kynakee is an AI-powered construction budget generation platform that transforms a site visit into a professional commercial offer in under 30 minutes. It serves construction professionals (reformadores, PYME constructoras, promotoras) who currently spend 4-8 hours manually creating budgets using Excel or outdated desktop software.

The platform operates through a conversational interface (WhatsApp, Telegram, Web App) and automates the 9-phase budget generation process: Capture → Context → Scope → Production → Planning → Valuation → Review → Offer. Each project feeds a Global APU Knowledge Base that improves accuracy and reduces cost for all users over time.

| Attribute | Value |
|---|---|
| Product Name | Kynakee |
| Product Type | SaaS Platform (Web + Bot) |
| Primary Market | Spain (EU) — LATAM expansion Month 10 |
| Target Segment | Reformadores independientes, PYMEs de reformas, constructoras medianas |
| Business Model | Credit-based SaaS (Starter €29/mo to Business €499/mo) |
| MVP Timeline | 1 month (Sprint 0) |
| Revenue Target (Month 12) | €22.000 MRR / 320 paying customers |

---

## 2. Problem Statement {#s2}

### The Current Pain

Construction professionals in Spain spend 4-8 hours creating a single budget. The process is manual, error-prone, and requires specialized knowledge that takes years to acquire. The tools available are either too complex (Presto, Arquimedes) or too generic (Excel).

| Pain Point | Impact |
|---|---|
| Manual APU creation | 4-8 hours per budget. High error rate. Requires expert knowledge. |
| No real-time pricing | Prices from static tables (CYPE updates quarterly). Budgets are inaccurate. |
| No mobile workflow | Professionals visit sites with paper and photos. Data entry happens later in office. |
| No conversational interface | Existing tools require training. WhatsApp is the primary communication tool. |
| No knowledge sharing | Each professional maintains their own APU database. No collective intelligence. |
| Slow offer generation | Client waits 2-5 days for a budget. Competitors win while waiting. |

### The Opportunity

- Spain has 1.9 million residential reforms per year (Andimac 2025).
- Only 12.4% of construction PYMEs use cloud software (Jotelulu Observatory 2025).
- The average reformador loses 2-3 projects per month due to slow budget delivery.
- No conversational AI budget tool exists for the Spanish construction market.

---

## 3. Product Vision {#s3}

> **Vision:** Kynakee is the operating system of construction. Every reformador, PYME, and promotora uses Kynakee to generate budgets, plan projects, and win clients — from their phone, in minutes, not days.

### Core Value Proposition

| For whom | Value |
|---|---|
| Reformador independiente | From 4-8 hours to 30 minutes per budget. Win more projects. |
| PYME de reformas | Scale without hiring more estimators. Consistent quality. |
| Constructora mediana | Standardize APU library. Reduce estimation errors. Track profitability. |
| Promotora | Fast preliminary budgets for feasibility studies. |
| Particular (homeowner) | Understand what a reform costs before hiring a professional. |

### Differentiators vs. Competition

| Feature | Kynakee | Presto / Arquimedes |
|---|---|---|
| Conversational interface | WhatsApp + Telegram + Web | Desktop only |
| Real-time pricing | MCP provider network | Static quarterly tables |
| AI-powered APU generation | Yes (DeepSeek-V3) | No |
| Global Knowledge Base | Shared across all users | Per-company only |
| Mobile-first capture | Photos + video + audio | Manual data entry |
| Time to first budget | < 30 minutes | 4-8 hours |
| Price | From €29/month | From €475/year (Arquimedes) |

---

## 4. Target Users & Personas {#s4}

### Persona 1: Carlos — Reformador Independiente (Primary)

| Attribute | Description |
|---|---|
| Age / Location | 38 years old, Valencia, Spain |
| Business | Self-employed reformador, 1-3 workers |
| Revenue | €80.000-€150.000/year |
| Current tools | Excel + WhatsApp + photos on phone |
| Pain | Spends Sunday evenings creating budgets. Loses clients who want fast answers. |
| Goal | Send a professional budget within 2 hours of the site visit. |
| Channel preference | WhatsApp (uses it for everything) |
| Tech comfort | Medium. Uses smartphone fluently. Avoids complex software. |

### Persona 2: María — Gerente de PYME de Reformas (Secondary)

| Attribute | Description |
|---|---|
| Age / Location | 45 years old, Madrid, Spain |
| Business | PYME de reformas, 8 employees, 2 estimators |
| Revenue | €500.000-€1.2M/year |
| Current tools | Presto + Excel + WhatsApp |
| Pain | Estimators are the bottleneck. Each budget takes 2 days. Inconsistent quality. |
| Goal | Standardize APU library. Let technicians generate budgets without senior estimator. |
| Channel preference | Web App (office) + WhatsApp (field) |
| Tech comfort | High. Uses Presto. Wants better, not simpler. |

### Persona 3: Javier — Particular (Tertiary)

| Attribute | Description |
|---|---|
| Age / Location | 52 years old, Barcelona, Spain |
| Situation | Homeowner planning a kitchen reform |
| Pain | Received 3 budgets with wildly different prices. Cannot evaluate them. |
| Goal | Understand what a reform should cost before hiring a professional. |
| Channel preference | Web App |
| Tech comfort | Medium. Uses smartphone and laptop. |

---

## 5. Market Context {#s5}

### Spain Market Size

- 1.9 million residential reforms per year (Andimac 2025)
- ~500.000 construction companies in Spain, 85% are microempresas (<10 employees)
- Only 12.4% of construction PYMEs use cloud software (vs 26% average for small businesses)
- Market growing: +1.6% YoY, +7.5% in rehabilitation permits (Andimac 2025)
- 4.5 million homes >50 years old needing intervention

### Go-to-Market Strategy

| Phase | Segment | Channel |
|---|---|---|
| Months 0-6 | Reformadores independientes | WhatsApp viral loop + gremio communities |
| Months 6-12 | PYMEs de reformas (10-50 employees) | Sales-assisted inbound + demos |
| Months 10-18 | LATAM (Mexico, Colombia) | Partner distributors + digital |
| Months 13-18 | Constructoras medianas + promotoras | Enterprise sales |

---

## 6. Core Features & Functional Requirements {#s6}

### FR-001: Multi-Channel Access

Users must be able to access all core functionality from WhatsApp, Telegram, and the Web App.

- **FR-001.1:** WhatsApp Bot via Meta Cloud API (free up to 1,000 conversations/month)
- **FR-001.2:** Telegram Bot via Telegram.Bot NuGet library
- **FR-001.3:** Web App via Next.js 15 (App Router)
- **FR-001.4:** SPA (System of Project Active): one active project per conversation
- **FR-001.5:** Conversation timeout: 30 min → Paused. 24h → resume prompt.

### FR-002: Multi-Tenant Architecture

- **FR-002.1:** Every tenant has isolated data (TenantId on all entities)
- **FR-002.2:** Tenant types: Individual, SME, Company, Promoter, Particular
- **FR-002.3:** User roles: Owner, Admin, Technician, Commercial, Viewer
- **FR-002.4:** CompanySettings per tenant: margins, profit, VAT, contingency

### FR-003: Credit-Based Economy

- **FR-003.1:** Plans: Starter (€29/500 credits), Pro (€89/2000), Studio (€199/6000), Business (€499/20000)
- **FR-003.2:** Credits consumed per AI operation (see consumption table in ADR-013)
- **FR-003.3:** TokenGate: reserve credits before AI operation, consume on success, release on failure
- **FR-003.4:** Extra credit packs purchasable at any time via Stripe
- **FR-003.5:** Credits do not expire within 3 months of purchase
- **FR-003.6:** Alerts at 75% and 90% consumption. Block at 100%.

### FR-004: AI-Powered Budget Generation

- **FR-004.1:** CaptureAgent: analyze photos/videos, extract work items automatically
- **FR-004.2:** ScopeAgent: map extracted items to canonical concepts
- **FR-004.3:** ProductionAgent: generate APU structures from KnowledgeBase or new
- **FR-004.4:** PlanningAgent: generate PERT/Gantt schedule automatically
- **FR-004.5:** ValuationAgent: price each APU component via MCP network
- **FR-004.6:** OfferAgent: generate professional commercial narrative
- **FR-004.7:** All AI operations must be explainable (AI Act Art. 13)

### FR-005: MCP Provider Network

- **FR-005.1:** Real-time price queries to integrated providers via JSON-RPC 2.0
- **FR-005.2:** Geolocation-based provider selection (nearest providers first)
- **FR-005.3:** Fallback chain per component: MCP → Cache → Internet
- **FR-005.4:** Provider compensation: fraction of token cost per successful query
- **FR-005.5:** Auto-suspension after 3 consecutive failures

### FR-006: Human Oversight (AI Act Compliance)

- **FR-006.1:** Phase 7 (Review) is MANDATORY and cannot be skipped (AI Act Art. 14)
- **FR-006.2:** All AI-generated content marked until human validation
- **FR-006.3:** Confidence level visible on every AI-generated item
- **FR-006.4:** Bot identifies as AI on first message (AI Act Art. 50)
- **FR-006.5:** Every AI operation logged in AgentRun table (AI Act Art. 12)

---

## 7. The 9-Phase Operational Flow {#s7}

The core product experience is a sequential 9-phase workflow managed by the Project aggregate. Each phase has specific inputs, outputs, and acceptance criteria.

| Phase | Name | Input | Output |
|---|---|---|---|
| 0 | Initialization | Location, client name, NIF | Project created with unique ID |
| 1 | Capture | Photos, videos, audio, measurements | CaptureExpedient with analyzed media |
| 2 | Context | Project location (auto) | Normative, labor, economic context |
| 3 | Scope | CaptureExpedient + AI | WorkItems[] with canonical concepts |
| 4 | Production | WorkItems + KnowledgeBase | APUAssignments[] with components |
| 5 | Planning | APUAssignments + Context | Schedule with PERT/Gantt |
| 6 | Valuation | APUAssignments + MCP | Priced components + total cost |
| 7 | Review | Valuation + Human | Approved changes + AI Act log |
| 8 | Offer | Review + AI | PDF offer ready to send |

> **⚠️ Invariant:** Modifying a WorkItem in Phase 3 automatically invalidates Valuation (Phase 6), Schedule (Phase 5), Review (Phase 7), and Offer (Phase 8). This is enforced by the Project aggregate and cannot be bypassed.

---

## 8. Non-Functional Requirements {#s8}

| Category | Requirement | Target |
|---|---|---|
| Performance | API response time (non-AI) | < 200ms p95 |
| Performance | AI operation completion | < 30s p95 |
| Performance | PDF generation | < 10s |
| Availability | System uptime (production) | > 99.5% |
| Scalability | Concurrent users (MVP) | 50 concurrent |
| Security | Authentication | JWT Bearer, 15min access token |
| Security | Data isolation | 100% tenant isolation via TenantId |
| Security | HTTPS | Enforced via Traefik, HSTS enabled |
| Compliance | AI Act | Art. 4, 12, 13, 14, 50 compliant |
| Compliance | GDPR | RGPD + LOPDGDD + LSSI-CE |
| Quality | Test coverage | > 80% (line + branch + method) |
| Maintainability | Module extraction | Each module extractable to microservice |

---

## 9. User Stories by Persona {#s9}

### Carlos — Reformador Independiente

- As Carlos, I want to create a project from WhatsApp so that I can start capturing evidence immediately after the site visit.
- As Carlos, I want to send photos and videos from my phone so that the AI can extract work items automatically.
- As Carlos, I want to receive a complete budget in under 30 minutes so that I can send it to the client the same day.
- As Carlos, I want to ask "why is this price?" in WhatsApp so that I can understand and justify the budget to my client.
- As Carlos, I want to see my credit balance so that I know how many more budgets I can generate this month.
- As Carlos, I want the offer PDF sent directly to my WhatsApp so that I can forward it to the client immediately.

### María — Gerente de PYME

- As María, I want to invite my technicians to the platform so that they can generate budgets without my supervision.
- As María, I want to configure company margins and profit percentages so that all budgets reflect our pricing strategy.
- As María, I want to see all projects across my team in a dashboard so that I can track progress and costs.
- As María, I want our APU library to grow with each project so that future budgets are faster and more accurate.
- As María, I want to review and approve budgets before they are sent so that I maintain quality control.

### Javier — Particular

- As Javier, I want to create a project without technical knowledge so that I can understand what my reform will cost.
- As Javier, I want the system to explain each cost item in plain language so that I can evaluate professional quotes.
- As Javier, I want to receive a recommendation to hire a professional for complex projects so that I make informed decisions.

---

## 10. Acceptance Criteria {#s10}

### AC-001: Project Creation

- **GIVEN** a registered user on WhatsApp, **WHEN** they send their location and project name, **THEN** a project is created with a unique ID and the user receives confirmation within 5 seconds.
- **GIVEN** a project in Initialization phase, **WHEN** the user sends photos, **THEN** the system processes them and extracts work items with confidence scores.

### AC-002: AI Budget Generation

- **GIVEN** a project with capture evidence, **WHEN** the user requests scope extraction, **THEN** the system returns work items within 30 seconds with confidence > 0.7 for standard items.
- **GIVEN** a project with work items, **WHEN** the user requests valuation, **THEN** each component has a price from MCP, cache, or internet with a fallback indicator.
- **GIVEN** a completed valuation, **WHEN** the user approves the review, **THEN** the system generates a PDF offer within 10 seconds.

### AC-003: Credit System

- **GIVEN** a user with 50 credits, **WHEN** they initiate an AI operation requiring 60 credits, **THEN** the operation is blocked and the user receives options (recharge, economic mode, upgrade).
- **GIVEN** a failed AI operation, **WHEN** credits were reserved, **THEN** credits are automatically released within 60 seconds.

### AC-004: Human Oversight (AI Act)

- **GIVEN** a completed valuation, **WHEN** the user tries to generate an offer without completing Phase 7 review, **THEN** the system blocks the operation and requires explicit human confirmation.
- **GIVEN** any AI-generated content, **WHEN** displayed to the user, **THEN** it is marked as "Generated by AI" until the user validates it in Phase 7.

---

## 11. Out of Scope (MVP) {#s11}

The following features are explicitly excluded from the MVP (Sprint 0 + Sprint 1):

- BIM integration (IFC file import) — Phase 8
- ERP integration (SAP, Holded, Sage) — Phase 8
- Multi-workspace (delegaciones regionales) — Phase 8
- Advanced analytics and profitability reports — Phase 6
- White-label option — Phase 8
- LATAM market (Mexico, Colombia) — Phase 7
- iOS/Android native app — Not planned (PWA via Next.js is sufficient)
- Video call integration for remote site visits — Not planned
- Automated invoice generation — Phase 6
- Project execution tracking (post-offer) — Phase 8

---

## 12. Success Metrics & KPIs {#s12}

### Product Metrics

| Metric | Target (Month 6) | Target (Month 12) |
|---|---|---|
| Time to first budget | < 30 minutes | < 20 minutes |
| Budget completion rate | > 60% | > 75% |
| User retention (30-day) | > 40% | > 55% |
| NPS (Net Promoter Score) | > 40 | > 50 |
| APUs in Knowledge Base | 500+ | 2.000+ |
| MCP providers integrated | 5 | 20+ |

### Business Metrics

| Metric | Target (Month 6) | Target (Month 12) |
|---|---|---|
| Paying customers | 80 | 320 |
| MRR | €5.500 | €22.000 |
| CAC (Customer Acquisition Cost) | < €50 | < €40 |
| LTV (Lifetime Value) | > €500 | > €800 |
| Churn rate (monthly) | < 5% | < 4% |
| Free-to-paid conversion | > 8% | > 10% |

### North Star Metric

> **North Star:** Number of APUs in the Global Knowledge Base. This single metric captures both user adoption (more projects = more APUs contributed) and product quality (more APUs = better accuracy = lower cost per project = more competitive pricing = more users). Target: 500 APUs by Month 6, 2.000 by Month 12, 10.000 by Month 18.

---

*KYNAKEE PLATFORM · Product Requirements Document v1.0 · 2026-08-20*  
*Confidential · For internal use only*
