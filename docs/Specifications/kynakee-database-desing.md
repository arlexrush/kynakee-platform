# KYNAKEE PLATFORM
## Database Design
### PostgreSQL 17 Schemas · EF Core Configuration · Qdrant Collections
**Version 1.0 · 2026-08-20 · Confidential**

---

## Table of Contents

- [1. Database Architecture Overview](#s1)
- [2. Global Conventions](#s2)
- [3. schema_projects — Core Domain](#s3)
- [4. schema_knowledge_base — APU Library](#s4)
- [5. schema_mcp — Provider Network](#s5)
- [6. schema_ai — Agent Audit Trail](#s6)
- [7. schema_bots — Conversational Channels](#s7)
- [8. schema_billing — Credit Economy](#s8)
- [9. schema_identity — Multi-Tenant Auth](#s9)
- [10. schema_shared — Cross-Module Infrastructure](#s10)
- [11. Qdrant Vector Collections](#s11)
- [12. EF Core Configuration Patterns](#s12)
- [13. Migration Strategy](#s13)
- [14. Indexing Strategy](#s14)

---

## 1. Database Architecture Overview {#s1}

Kynakee uses two database technologies: **PostgreSQL 17** as the primary relational database and **Qdrant** as the vector database for semantic search. Each of the 7 modules owns its own PostgreSQL schema, enforcing logical isolation without the operational complexity of separate database instances.

| Schema | Module | Purpose |
|---|---|---|
| schema_projects | Projects (Core) | All project lifecycle data: phases, work items, APU assignments, schedules, valuations, reviews, offers |
| schema_knowledge_base | KnowledgeBase | Global APU template library and canonical concept registry |
| schema_mcp | MCP | MCP provider registry and query audit logs |
| schema_ai | AI | Agent run audit trail for AI Act compliance |
| schema_bots | Bots | Bot conversation state and message history |
| schema_billing | Billing | Credit accounts, transactions, subscriptions, Stripe events |
| schema_identity | Identity | Tenants, users, roles, refresh tokens |
| schema_shared | Shared | Outbox messages, idempotency keys, inbox messages |

> **⚠️ Rule:** Modules NEVER access another module's schema directly. Cross-module data access is ONLY via integration events (RabbitMQ) or public module interfaces (in-process MediatR).

---

## 2. Global Conventions {#s2}

### Base Table Structure

Every table in every schema MUST include the following columns, enforced by the EF Core `AuditInterceptor`:

```sql
-- Mandatory columns on ALL tables
id              UUID PRIMARY KEY DEFAULT gen_random_uuid()
tenant_id       UUID NOT NULL,                    -- Multi-tenant isolation
created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
created_by      UUID,                             -- UserId (optional)
updated_by      UUID,                             -- UserId (optional)
deleted_at      TIMESTAMPTZ,                      -- Soft delete timestamp
is_deleted      BOOLEAN NOT NULL DEFAULT FALSE,   -- Soft delete flag
xmin            xid,                              -- PostgreSQL row version (optimistic concurrency)
```

### Naming Conventions

| Element | Convention |
|---|---|
| Schema names | snake_case: schema_projects, schema_billing |
| Table names | snake_case, plural: projects, work_items, credit_accounts |
| Column names | snake_case: tenant_id, created_at, is_deleted |
| Primary keys | id UUID (always) |
| Foreign keys | {table_singular}_id: project_id, work_item_id |
| Enum columns | VARCHAR(50) with CHECK constraint |
| JSON columns | JSONB for flexible structures |
| Array columns | TEXT[] or UUID[] for simple arrays |
| Index names | idx_{table}_{column(s)}: idx_projects_tenant_id |

### Soft Delete Pattern

```sql
-- EF Core global query filter (applied automatically):
WHERE is_deleted = FALSE AND tenant_id = @tenantId

-- Hard delete ONLY via GDPRDataErasureService:
DELETE FROM schema_identity.users WHERE id = @userId;  -- GDPR erasure only

-- Soft delete (standard):
UPDATE schema_projects.projects
SET is_deleted = TRUE, deleted_at = NOW(), updated_by = @userId
WHERE id = @projectId AND tenant_id = @tenantId;
```

---

## 3. schema_projects — Core Domain {#s3}

The largest and most complex schema. Contains all project lifecycle data across 9 phases.

### Table: projects

```sql
CREATE TABLE schema_projects.projects (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID NOT NULL,
    name            VARCHAR(300) NOT NULL,
    -- Client info (Value Object stored as columns)
    client_name     VARCHAR(200) NOT NULL,
    client_nif      VARCHAR(20),
    client_email    VARCHAR(200),
    client_phone    VARCHAR(30),
    -- Location (Value Object)
    country         VARCHAR(3) NOT NULL DEFAULT 'ES',
    region          VARCHAR(10),
    province        VARCHAR(100),
    municipality    VARCHAR(100),
    postal_code     VARCHAR(10),
    latitude        DECIMAL(9,6),
    longitude       DECIMAL(9,6),
    -- Phase state machine
    current_phase   VARCHAR(30) NOT NULL DEFAULT 'Initialization',
    status          VARCHAR(20) NOT NULL DEFAULT 'Active',
    channel         VARCHAR(20) NOT NULL,  -- web|telegram|whatsapp
    -- Token consumption
    total_tokens    INTEGER NOT NULL DEFAULT 0,
    total_credits   DECIMAL(10,4) NOT NULL DEFAULT 0,
    -- Audit (mandatory)
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by      UUID,
    updated_by      UUID,
    deleted_at      TIMESTAMPTZ,
    is_deleted      BOOLEAN NOT NULL DEFAULT FALSE,
    xmin            xid
);
CREATE INDEX idx_projects_tenant_id ON schema_projects.projects(tenant_id) WHERE is_deleted = FALSE;
CREATE INDEX idx_projects_status ON schema_projects.projects(tenant_id, status) WHERE is_deleted = FALSE;
```

### Table: work_items (Phase 3 — Partidas)

```sql
CREATE TABLE schema_projects.work_items (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    project_id          UUID NOT NULL REFERENCES schema_projects.projects(id),
    tenant_id           UUID NOT NULL,
    canonical_concept_id VARCHAR(100) NOT NULL,  -- e.g. PART_TILE_WALL_PORCELAIN
    description         VARCHAR(500) NOT NULL,
    unit                VARCHAR(20) NOT NULL,    -- m2|m3|ml|ud|kg|h|day
    quantity            DECIMAL(12,4) NOT NULL,
    location            VARCHAR(200),
    observations        TEXT,
    confidence          DECIMAL(4,3),            -- 0.000 to 1.000
    ai_status           VARCHAR(30) NOT NULL DEFAULT 'GeneratedByAI',
    sort_order          INTEGER NOT NULL DEFAULT 0,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by          UUID,
    updated_by          UUID,
    deleted_at          TIMESTAMPTZ,
    is_deleted          BOOLEAN NOT NULL DEFAULT FALSE
);
CREATE INDEX idx_work_items_project ON schema_projects.work_items(project_id) WHERE is_deleted = FALSE;
CREATE INDEX idx_work_items_concept ON schema_projects.work_items(canonical_concept_id);
```

### Table: apu_assignments (Phase 4)

```sql
CREATE TABLE schema_projects.apu_assignments (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    project_id          UUID NOT NULL REFERENCES schema_projects.projects(id),
    work_item_id        UUID NOT NULL REFERENCES schema_projects.work_items(id),
    tenant_id           UUID NOT NULL,
    apu_template_id     UUID NOT NULL,  -- Reference to schema_knowledge_base.apu_templates
    source              VARCHAR(30) NOT NULL,  -- Cached|Revalued|GeneratedNew
    unit_price          DECIMAL(12,4),         -- NULL until Phase 6 valuation
    currency            VARCHAR(3) NOT NULL DEFAULT 'EUR',
    confidence          DECIMAL(4,3),
    components          JSONB NOT NULL DEFAULT '[]',  -- APUComponent[]
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by          UUID,
    updated_by          UUID,
    deleted_at          TIMESTAMPTZ,
    is_deleted          BOOLEAN NOT NULL DEFAULT FALSE
);
-- components JSONB structure:
-- [{ "description": "Porcelain 60x60", "type": "Material", "unit": "m2",
--    "yield": 1.05, "unit_price": 28.50, "fallback_indicator": null }]
```

### Table: schedules (Phase 5)

```sql
CREATE TABLE schema_projects.schedules (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    project_id          UUID NOT NULL REFERENCES schema_projects.projects(id),
    tenant_id           UUID NOT NULL,
    total_duration_days INTEGER NOT NULL,
    start_date          DATE,
    end_date            DATE,
    activities          JSONB NOT NULL DEFAULT '[]',
    precedences         JSONB NOT NULL DEFAULT '[]',
    critical_path       UUID[] NOT NULL DEFAULT '{}',
    milestones          JSONB NOT NULL DEFAULT '[]',
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    is_deleted          BOOLEAN NOT NULL DEFAULT FALSE
);
-- activities JSONB: [{ "id": "uuid", "name": "Demolition", "duration_days": 3, "work_item_ids": [...] }]
-- precedences JSONB: [{ "activity_id": "uuid", "predecessor_id": "uuid", "type": "FS", "lag": 0 }]
```

### Table: valuations (Phase 6)

```sql
CREATE TABLE schema_projects.valuations (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    project_id          UUID NOT NULL REFERENCES schema_projects.projects(id),
    tenant_id           UUID NOT NULL,
    direct_cost         DECIMAL(14,4) NOT NULL DEFAULT 0,
    indirect_cost       DECIMAL(14,4) NOT NULL DEFAULT 0,
    administration      DECIMAL(14,4) NOT NULL DEFAULT 0,
    quality             DECIMAL(14,4) NOT NULL DEFAULT 0,
    safety_health       DECIMAL(14,4) NOT NULL DEFAULT 0,
    environment         DECIMAL(14,4) NOT NULL DEFAULT 0,
    contingency         DECIMAL(14,4) NOT NULL DEFAULT 0,
    profit              DECIMAL(14,4) NOT NULL DEFAULT 0,
    vat                 DECIMAL(14,4) NOT NULL DEFAULT 0,
    total_cost          DECIMAL(14,4) NOT NULL DEFAULT 0,
    currency            VARCHAR(3) NOT NULL DEFAULT 'EUR',
    confidence_level    DECIMAL(4,3),
    is_complete         BOOLEAN NOT NULL DEFAULT FALSE,
    valued_at           TIMESTAMPTZ,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    is_deleted          BOOLEAN NOT NULL DEFAULT FALSE
);
```

### Table: reviews (Phase 7)

```sql
CREATE TABLE schema_projects.reviews (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    project_id          UUID NOT NULL REFERENCES schema_projects.projects(id),
    tenant_id           UUID NOT NULL,
    reviewer_id         UUID NOT NULL,
    is_approved         BOOLEAN NOT NULL DEFAULT FALSE,
    approved_at         TIMESTAMPTZ,
    changes             JSONB NOT NULL DEFAULT '[]',
    ai_act_log          JSONB NOT NULL DEFAULT '{}',  -- AI Act Art. 12 compliance
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    is_deleted          BOOLEAN NOT NULL DEFAULT FALSE
);
-- ai_act_log JSONB: { "supervisor_human": "user-uuid", "timestamp": "...",
--   "items_reviewed": 12, "items_modified": 2, "ai_act_art14_confirmed": true }
```

### Table: offers (Phase 8)

```sql
CREATE TABLE schema_projects.offers (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    project_id          UUID NOT NULL REFERENCES schema_projects.projects(id),
    tenant_id           UUID NOT NULL,
    version             INTEGER NOT NULL DEFAULT 1,
    total_amount        DECIMAL(14,4) NOT NULL,
    currency            VARCHAR(3) NOT NULL DEFAULT 'EUR',
    conditions          TEXT,
    warranties          TEXT,
    validity_days       INTEGER NOT NULL DEFAULT 30,
    pdf_url             VARCHAR(500),
    status              VARCHAR(20) NOT NULL DEFAULT 'Draft',
    sent_at             TIMESTAMPTZ,
    ai_disclaimer       TEXT,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    is_deleted          BOOLEAN NOT NULL DEFAULT FALSE
);
```

### Table: capture_expedients (Phase 1)

```sql
CREATE TABLE schema_projects.capture_expedients (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    project_id          UUID NOT NULL REFERENCES schema_projects.projects(id),
    tenant_id           UUID NOT NULL,
    media_files         JSONB NOT NULL DEFAULT '[]',
    measurements        JSONB NOT NULL DEFAULT '[]',
    transcriptions      JSONB NOT NULL DEFAULT '[]',
    observations        JSONB NOT NULL DEFAULT '[]',
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    is_deleted          BOOLEAN NOT NULL DEFAULT FALSE
);
-- media_files JSONB: [{ "url": "...", "type": "video|image|audio|document",
--   "estancia": "bathroom", "is_pathology": false, "processed": true }]
-- measurements JSONB: [{ "description": "...", "value": 24.5, "unit": "m2" }]
```

### Table: project_contexts (Phase 2)

```sql
CREATE TABLE schema_projects.project_contexts (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    project_id          UUID NOT NULL REFERENCES schema_projects.projects(id),
    tenant_id           UUID NOT NULL,
    -- Territorial
    country             VARCHAR(3),
    region              VARCHAR(10),
    province            VARCHAR(100),
    municipality        VARCHAR(100),
    -- Normative
    urban_regulation    VARCHAR(200),
    construction_code   VARCHAR(200),
    -- Labor
    collective_agreement VARCHAR(300),
    salary_official_1   DECIMAL(8,2),
    salary_laborer      DECIMAL(8,2),
    -- Economic
    inflation_rate      DECIMAL(5,2),
    vat_rate            DECIMAL(5,2),
    construction_index  DECIMAL(8,4),
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    is_deleted          BOOLEAN NOT NULL DEFAULT FALSE
);
```

---

## 4. schema_knowledge_base — APU Library {#s4}

Global APU template library shared across all tenants.

### Table: canonical_concepts

```sql
CREATE TABLE schema_knowledge_base.canonical_concepts (
    id                  VARCHAR(100) PRIMARY KEY,  -- e.g. PART_TILE_WALL_PORCELAIN
    tenant_id           UUID,                      -- NULL = global concept
    category            VARCHAR(100) NOT NULL,
    subcategory         VARCHAR(100),
    default_unit        VARCHAR(20) NOT NULL,
    apu_template_count  INTEGER NOT NULL DEFAULT 0,
    embedding_vector    vector(768),               -- pgvector extension
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    is_deleted          BOOLEAN NOT NULL DEFAULT FALSE
);

CREATE TABLE schema_knowledge_base.concept_translations (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    concept_id          VARCHAR(100) NOT NULL REFERENCES schema_knowledge_base.canonical_concepts(id),
    language_code       VARCHAR(5) NOT NULL,  -- es|en|fr|pt|de
    name                VARCHAR(300) NOT NULL,
    description         TEXT,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE UNIQUE INDEX idx_concept_translations_unique ON schema_knowledge_base.concept_translations(concept_id, language_code);
```

### Table: apu_templates

```sql
CREATE TABLE schema_knowledge_base.apu_templates (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id           UUID,                      -- NULL = global template
    canonical_concept_id VARCHAR(100) NOT NULL REFERENCES schema_knowledge_base.canonical_concepts(id),
    description         VARCHAR(500) NOT NULL,
    project_type        VARCHAR(30) NOT NULL,      -- Residential|Commercial|Industrial
    geo_region          VARCHAR(10) NOT NULL,      -- ES-VC|ES-CT|ES-MD|...
    unit                VARCHAR(20) NOT NULL,
    yield_hours_per_unit DECIMAL(8,4) NOT NULL,
    crew_description    VARCHAR(200),
    usage_count         INTEGER NOT NULL DEFAULT 0,
    average_confidence  DECIMAL(4,3),
    source              VARCHAR(30) NOT NULL DEFAULT 'GeneratedByAI',
    embedding_vector    vector(768),
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    is_deleted          BOOLEAN NOT NULL DEFAULT FALSE
);
CREATE INDEX idx_apu_templates_concept ON schema_knowledge_base.apu_templates(canonical_concept_id, geo_region);

CREATE TABLE schema_knowledge_base.apu_template_components (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    apu_template_id     UUID NOT NULL REFERENCES schema_knowledge_base.apu_templates(id),
    description         VARCHAR(300) NOT NULL,
    component_type      VARCHAR(20) NOT NULL,  -- Material|Labor|Equipment|Subcontract|Transport
    unit                VARCHAR(20) NOT NULL,
    yield               DECIMAL(10,6) NOT NULL,  -- Quantity per unit of work item
    sort_order          INTEGER NOT NULL DEFAULT 0
    -- NOTE: NO price column here. Prices are determined per-project in Phase 6.
);
```

---

## 5. schema_mcp — Provider Network {#s5}

### Table: mcp_providers

```sql
CREATE TABLE schema_mcp.mcp_providers (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id           UUID,                      -- NULL = global provider
    name                VARCHAR(200) NOT NULL,
    subdomain           VARCHAR(100) NOT NULL UNIQUE,
    endpoint            VARCHAR(500) NOT NULL,     -- https://{subdomain}.kynakee.com/mcp
    api_key_hash        VARCHAR(500) NOT NULL,     -- bcrypt hash
    categories          TEXT[] NOT NULL DEFAULT '{}',
    geo_regions         TEXT[] NOT NULL DEFAULT '{}',
    status              VARCHAR(20) NOT NULL DEFAULT 'Active',
    rating              DECIMAL(3,2) NOT NULL DEFAULT 5.00,
    consecutive_failures INTEGER NOT NULL DEFAULT 0,
    last_success_at     TIMESTAMPTZ,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    is_deleted          BOOLEAN NOT NULL DEFAULT FALSE
);
CREATE INDEX idx_mcp_providers_region ON schema_mcp.mcp_providers USING GIN(geo_regions);
CREATE INDEX idx_mcp_providers_category ON schema_mcp.mcp_providers USING GIN(categories);
```

### Table: mcp_query_logs

```sql
CREATE TABLE schema_mcp.mcp_query_logs (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id           UUID NOT NULL,
    project_id          UUID NOT NULL,
    provider_id         UUID NOT NULL REFERENCES schema_mcp.mcp_providers(id),
    canonical_concept_id VARCHAR(100) NOT NULL,
    component_type      VARCHAR(20) NOT NULL,
    quantity            DECIMAL(12,4),
    unit                VARCHAR(20),
    response_price      DECIMAL(12,4),
    response_unit       VARCHAR(20),
    fallback_activated  BOOLEAN NOT NULL DEFAULT FALSE,
    fallback_source     VARCHAR(30),  -- Cache|Internet
    confidence          DECIMAL(4,3),
    duration_ms         INTEGER,
    status              VARCHAR(20) NOT NULL,  -- Success|Timeout|Error|Fallback
    credits_charged     DECIMAL(10,4) NOT NULL DEFAULT 0,
    queried_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_mcp_query_logs_project ON schema_mcp.mcp_query_logs(project_id, queried_at DESC);
```

---

## 6. schema_ai — Agent Audit Trail {#s6}

Records every AI agent run for billing, observability, and AI Act Art. 12 compliance.

### Table: agent_runs

```sql
CREATE TABLE schema_ai.agent_runs (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id           UUID NOT NULL,
    project_id          UUID NOT NULL,
    agent_type          VARCHAR(30) NOT NULL,  -- Capture|Scope|Production|Planning|Valuation|Offer|Conversation|Embedding
    model_id            VARCHAR(50) NOT NULL,  -- deepseek-v3|gemini-flash|gemma4|...
    provider            VARCHAR(30) NOT NULL,  -- DeepSeek|Gemini|Gemma|OpenAI
    input_tokens        INTEGER NOT NULL DEFAULT 0,
    output_tokens       INTEGER NOT NULL DEFAULT 0,
    credits_charged     DECIMAL(10,4) NOT NULL DEFAULT 0,
    fallback_activated  BOOLEAN NOT NULL DEFAULT FALSE,
    fallback_reason     VARCHAR(200),
    status              VARCHAR(20) NOT NULL,  -- Success|Failed|FallbackUsed
    duration_ms         INTEGER,
    human_reviewed      BOOLEAN NOT NULL DEFAULT FALSE,  -- AI Act Art. 14
    human_reviewed_at   TIMESTAMPTZ,
    human_reviewer_id   UUID,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_agent_runs_project ON schema_ai.agent_runs(project_id, created_at DESC);
CREATE INDEX idx_agent_runs_tenant ON schema_ai.agent_runs(tenant_id, created_at DESC);
```

---

## 7. schema_bots — Conversational Channels {#s7}

### Table: bot_conversations

```sql
CREATE TABLE schema_bots.bot_conversations (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id           UUID NOT NULL,
    external_id         VARCHAR(100) NOT NULL,  -- Phone (WA) or ChatId (TG)
    channel             VARCHAR(20) NOT NULL,   -- Telegram|WhatsApp
    user_id             UUID,                   -- NULL if not yet registered
    active_project_id   UUID,                   -- SPA: System of Project Active
    state               VARCHAR(30) NOT NULL DEFAULT 'NewSession',
    verbosity           VARCHAR(20) NOT NULL DEFAULT 'Normal',
    last_interaction_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    is_deleted          BOOLEAN NOT NULL DEFAULT FALSE
);
CREATE UNIQUE INDEX idx_bot_conversations_external ON schema_bots.bot_conversations(external_id, channel) WHERE is_deleted = FALSE;
```

### Table: bot_messages

```sql
CREATE TABLE schema_bots.bot_messages (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    conversation_id     UUID NOT NULL REFERENCES schema_bots.bot_conversations(id),
    tenant_id           UUID NOT NULL,
    direction           VARCHAR(10) NOT NULL,  -- Inbound|Outbound
    message_type        VARCHAR(20) NOT NULL,  -- Text|Image|Audio|Video|Document|Command
    content             TEXT,
    media_url           VARCHAR(500),
    parsed_command      VARCHAR(100),
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_bot_messages_conversation ON schema_bots.bot_messages(conversation_id, created_at DESC);
```

---

## 8. schema_billing — Credit Economy {#s8}

### Table: credit_accounts

```sql
CREATE TABLE schema_billing.credit_accounts (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id           UUID NOT NULL UNIQUE,
    plan_id             VARCHAR(50) NOT NULL,
    available_credits   DECIMAL(12,4) NOT NULL DEFAULT 0,
    reserved_credits    DECIMAL(12,4) NOT NULL DEFAULT 0,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    is_deleted          BOOLEAN NOT NULL DEFAULT FALSE,
    xmin                xid
);
```

### Table: credit_transactions

```sql
CREATE TABLE schema_billing.credit_transactions (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    credit_account_id   UUID NOT NULL REFERENCES schema_billing.credit_accounts(id),
    tenant_id           UUID NOT NULL,
    operation_id        UUID,                  -- Links Reserve/Consume/Release
    transaction_type    VARCHAR(20) NOT NULL,  -- Reserve|Consume|Release|Recharge
    amount              DECIMAL(10,4) NOT NULL,
    source              VARCHAR(100),          -- stripe|manual|promo|plan_renewal
    description         VARCHAR(300),
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_credit_tx_account ON schema_billing.credit_transactions(credit_account_id, created_at DESC);
CREATE INDEX idx_credit_tx_operation ON schema_billing.credit_transactions(operation_id);
```

### Table: subscriptions

```sql
CREATE TABLE schema_billing.subscriptions (
    id                      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id               UUID NOT NULL,
    plan_id                 VARCHAR(50) NOT NULL,
    status                  VARCHAR(20) NOT NULL DEFAULT 'Active',
    billing_cycle           VARCHAR(10) NOT NULL,  -- Monthly|Annual
    current_period_start    DATE NOT NULL,
    current_period_end      DATE NOT NULL,
    credits_per_cycle       INTEGER NOT NULL,
    stripe_subscription_id  VARCHAR(100),
    stripe_customer_id      VARCHAR(100),
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    is_deleted              BOOLEAN NOT NULL DEFAULT FALSE
);
```

### Table: stripe_events

```sql
CREATE TABLE schema_billing.stripe_events (
    id                  VARCHAR(100) PRIMARY KEY,  -- Stripe event ID
    tenant_id           UUID,
    event_type          VARCHAR(100) NOT NULL,
    payload             JSONB NOT NULL,
    processed           BOOLEAN NOT NULL DEFAULT FALSE,
    processed_at        TIMESTAMPTZ,
    error               TEXT,
    received_at         TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

---

## 9. schema_identity — Multi-Tenant Auth {#s9}

### Table: tenants

```sql
CREATE TABLE schema_identity.tenants (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id           UUID NOT NULL UNIQUE,  -- Same as id, for consistency
    name                VARCHAR(300) NOT NULL,
    slug                VARCHAR(100) NOT NULL UNIQUE,
    tenant_type         VARCHAR(20) NOT NULL,  -- Individual|SME|Company|Promoter
    tax_id              VARCHAR(20),
    fiscal_address      JSONB,
    plan_id             VARCHAR(50) NOT NULL DEFAULT 'plan_starter',
    status              VARCHAR(20) NOT NULL DEFAULT 'Active',
    branding            JSONB NOT NULL DEFAULT '{}',
    settings            JSONB NOT NULL DEFAULT '{}',  -- CompanySettings
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    is_deleted          BOOLEAN NOT NULL DEFAULT FALSE
);
-- settings JSONB: { "administration": 4, "profit": 8, "quality": 2,
--   "safety_health": 3, "environment": 1, "contingency": 3 }
```

### Table: users

```sql
CREATE TABLE schema_identity.users (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id           UUID NOT NULL,
    first_name          VARCHAR(100) NOT NULL,
    last_name           VARCHAR(100) NOT NULL,
    email               VARCHAR(200) NOT NULL,
    phone               VARCHAR(30),
    password_hash       VARCHAR(500) NOT NULL,
    status              VARCHAR(20) NOT NULL DEFAULT 'PendingVerification',
    telegram_chat_id    VARCHAR(50),
    whatsapp_phone      VARCHAR(30),
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    is_deleted          BOOLEAN NOT NULL DEFAULT FALSE
);
CREATE UNIQUE INDEX idx_users_email ON schema_identity.users(email) WHERE is_deleted = FALSE;

CREATE TABLE schema_identity.tenant_users (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id           UUID NOT NULL REFERENCES schema_identity.tenants(id),
    user_id             UUID NOT NULL REFERENCES schema_identity.users(id),
    role                VARCHAR(20) NOT NULL,  -- Owner|Admin|Technician|Commercial|Viewer
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE UNIQUE INDEX idx_tenant_users_unique ON schema_identity.tenant_users(tenant_id, user_id);

CREATE TABLE schema_identity.refresh_tokens (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id             UUID NOT NULL REFERENCES schema_identity.users(id),
    tenant_id           UUID NOT NULL,
    token_hash          VARCHAR(500) NOT NULL,
    expires_at          TIMESTAMPTZ NOT NULL,
    revoked_at          TIMESTAMPTZ,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

---

## 10. schema_shared — Cross-Module Infrastructure {#s10}

### Table: outbox_messages (MassTransit Outbox)

```sql
CREATE TABLE schema_shared.outbox_messages (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id           UUID NOT NULL,
    event_type          VARCHAR(200) NOT NULL,
    payload             JSONB NOT NULL,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    processed_at        TIMESTAMPTZ,
    error               TEXT,
    retry_count         INTEGER NOT NULL DEFAULT 0
);
-- Managed by MassTransit. Do NOT write to this table directly.
CREATE INDEX idx_outbox_unprocessed ON schema_shared.outbox_messages(created_at) WHERE processed_at IS NULL;
```

### Table: idempotency_keys

```sql
CREATE TABLE schema_shared.idempotency_keys (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    key                 VARCHAR(200) NOT NULL UNIQUE,  -- {event_type}:{event_id}
    tenant_id           UUID NOT NULL,
    processed_at        TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    handler             VARCHAR(200) NOT NULL
);
-- TTL: Clean up keys older than 30 days via Hangfire job.
CREATE INDEX idx_idempotency_key ON schema_shared.idempotency_keys(key);
```

---

## 11. Qdrant Vector Collections {#s11}

Qdrant is used exclusively by the KnowledgeBase module. All vectors are 768-dimensional embeddings generated by Gemini text-embedding-004.

### Collection: canonical_concepts

```json
{
  "name": "canonical_concepts",
  "vectors": { "size": 768, "distance": "Cosine" },
  "payload_schema": {
    "canonical_id":    "keyword",
    "category":        "keyword",
    "subcategory":     "keyword",
    "default_unit":    "keyword",
    "languages":       "object",
    "apu_count":       "integer",
    "avg_confidence":  "float",
    "regions":         "keyword[]",
    "last_updated":    "datetime"
  }
}
// Search: find canonical concept by natural language description in any language
// e.g. "porcelain wall tile" → PART_TILE_WALL_PORCELAIN (0.94 similarity)
```

### Collection: apu_structures

```json
{
  "name": "apu_structures",
  "vectors": { "size": 768, "distance": "Cosine" },
  "payload_schema": {
    "apu_template_id": "keyword",
    "canonical_id":    "keyword",
    "project_type":    "keyword",
    "geo_region":      "keyword",
    "unit":            "keyword",
    "component_types": "keyword[]",
    "usage_count":     "integer",
    "avg_confidence":  "float",
    "source":          "keyword"
  }
}
// Search: find similar APU templates for a given work item description + context
```

### Collection: project_contexts

```json
{
  "name": "project_contexts",
  "vectors": { "size": 768, "distance": "Cosine" },
  "payload_schema": {
    "context_id":      "keyword",
    "project_type":    "keyword",
    "geo_region":      "keyword",
    "building_type":   "keyword",
    "scope_summary":   "text",
    "apu_ids_used":    "keyword[]"
  }
}
// Search: find projects with similar context to suggest relevant APU templates
```

---

## 12. EF Core Configuration Patterns {#s12}

### DbContext per Module

```csharp
// Each module has its own DbContext
public class ProjectDbContext : DbContext
{
    public DbSet<Project> Projects { get; set; }
    public DbSet<WorkItem> WorkItems { get; set; }
    public DbSet<APUAssignment> APUAssignments { get; set; }
    // ... other phase entities

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("schema_projects");

        // Global query filters (applied automatically to ALL queries)
        modelBuilder.Entity<Project>().HasQueryFilter(
            p => !p.IsDeleted && p.TenantId == _tenantContext.TenantId);

        // Optimistic concurrency via PostgreSQL xmin
        modelBuilder.Entity<Project>()
            .UseXminAsConcurrencyToken();

        // Value Object owned entities
        modelBuilder.Entity<Project>().OwnsOne(p => p.Client);
        modelBuilder.Entity<Project>().OwnsOne(p => p.Location);
    }
}
```

### JSONB Column Mapping

```csharp
// Map JSONB columns to C# types
modelBuilder.Entity<APUAssignment>()
    .Property(a => a.Components)
    .HasColumnType("jsonb")
    .HasConversion(
        v => JsonSerializer.Serialize(v, null),
        v => JsonSerializer.Deserialize<List<APUComponent>>(v, null)!
    );
```

### Audit Interceptor Registration

```csharp
// Register in each module's DbContext
services.AddDbContext<ProjectDbContext>(options =>
{
    options.UseNpgsql(connectionString)
           .AddInterceptors(new AuditInterceptor(tenantContext));
});
```

---

## 13. Migration Strategy {#s13}

- Each module has its own migrations folder: `Kynakee.Modules.{Module}.Infrastructure.Persistence.Migrations/`.
- Migrations are applied at startup via `DbContext.Database.MigrateAsync()` in development only. Production migrations are applied as an explicit deployment step.
- Production migrations are applied manually via CLI before deployment: `dotnet ef database update`.
- Never modify the database directly. All schema changes via EF Core migrations.
- Migration naming: `{timestamp}_{description}` e.g. `20260820_AddWorkItemSortOrder`.

```csharp
// Apply migrations at startup (development only)
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<ProjectDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<BillingDbContext>().Database.MigrateAsync();
    // ... all module DbContexts
}
```

---

## 14. Indexing Strategy {#s14}

Every table must have the following minimum indexes. Additional indexes are added based on query patterns.

| Index Type | Columns | Applied to |
|---|---|---|
| Mandatory (all tables) | (tenant_id) WHERE is_deleted = FALSE | Every table in every schema |
| Mandatory (all tables) | (created_at DESC) | Every table for time-based queries |
| Foreign key | (project_id) | work_items, apu_assignments, schedules, valuations, reviews, offers |
| Unique | (email) WHERE is_deleted = FALSE | schema_identity.users |
| Unique | (slug) | schema_identity.tenants |
| Unique | (subdomain) | schema_mcp.mcp_providers |
| GIN (array) | (geo_regions), (categories) | schema_mcp.mcp_providers |
| GIN (JSONB) | (components) | schema_projects.apu_assignments (if queried) |
| Partial | (canonical_concept_id, geo_region) | schema_knowledge_base.apu_templates |

> **Performance Rule:** Never add indexes speculatively. Add indexes only when a slow query is identified via OpenTelemetry traces or EXPLAIN ANALYZE. Over-indexing degrades write performance.

---

*KYNAKEE PLATFORM · Database Design v1.0 · 2026-08-20*  
*Confidential · For internal development use only*
