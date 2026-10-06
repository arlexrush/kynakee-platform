# KYNAKEE PLATFORM
## Database Design
### PostgreSQL 17 Schemas · EF Core Configuration · Qdrant Collections
**Version 1.0 · 2026-08-20 · Confidential**

> **Implementation status:** This is a schema specification, not evidence of production deployment. See [current state and governance](../Gobernanza/Estado-y-gobernanza.md) for each module's verified status. Projects migrations and PostgreSQL persistence tests remain postponed. Other modules have differing implementation and verification states; do not treat this document alone as proof of compilation, migration application or operational behavior.

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
| schema_billing | Billing | Credit accounts, expiring credit lots, transaction ledger and subscriptions |
| schema_identity | Identity | Tenants, users, memberships, invitations and refresh tokens |
| schema_shared | Shared | Outbox messages, idempotency keys, inbox messages |

> **⚠️ Rule:** Modules NEVER access another module's schema directly. Cross-module data access is ONLY via integration events (RabbitMQ) or public module interfaces (in-process MediatR).

---

## 2. Global Conventions {#s2}

### Base Table Structure

Target convention for tenant-owned persistent tables: map the audit fields from `BaseEntity<TId>`. Genuinely global KnowledgeBase roots use `GlobalEntity<TId>` instead: no `tenant_id`, optional `owner_tenant_id` and `owner_user_id` for provenance, the same audit/soft-delete fields, and a soft-delete-only visibility filter. Global writes require explicit authorization. `xmin` and any `AuditInterceptor` require actual EF configuration and migration tests; this table is not proof that an interceptor or all schemas exist:

```sql
-- Mandatory columns on tenant-owned entity tables
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
| Primary keys | id UUID by default; canonical_concepts uses a VARCHAR(100) business identifier |
| Foreign keys | {table_singular}_id: project_id, work_item_id |
| Enum columns | VARCHAR(50) with CHECK constraint |
| JSON columns | JSONB for flexible structures |
| Array columns | TEXT[] or UUID[] for simple arrays |
| Index names | idx_{table}_{column(s)}: idx_projects_tenant_id |

### Soft Delete Pattern

```sql
-- EF Core global query filter (applied automatically):
WHERE is_deleted = FALSE AND tenant_id = @tenantId

-- Target filter for global KnowledgeBase roots (not yet implemented):
WHERE is_deleted = FALSE

-- Planned hard-delete policy only; no GDPRDataErasureService is evidenced as implemented:
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
    client_personal_identifier VARCHAR(20),
    client_business_identifier VARCHAR(20),
    client_kind     VARCHAR(20),
    client_tax_country VARCHAR(2),
    client_email    VARCHAR(200),
    client_phone    VARCHAR(30),
    -- Location (Value Object)
    country         VARCHAR(2) NOT NULL DEFAULT 'ES',
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
The following root tables are a target schema, not an applied migration. EF configurations, `KnowledgeBaseDbContext`, and EF repositories compile in the module; no migration or PostgreSQL round-trip has been verified. Optional owner identifiers record provenance and do not restrict reads or grant write access; global roots derive from `GlobalEntity<TId>` through `GlobalAggregateRoot<TId>` where event support is required, with no `TenantId`. Translations and components are dependent values belonging to their respective aggregates, not independent tenant-owned roots. Map PostgreSQL's system column `xmin` for concurrency; do not declare it in `CREATE TABLE`. Embeddings belong in Qdrant under ADR-010, not in an additional pgvector column. A Qdrant adapter compiles for concept/APU vector upsert, search, and removal, but live connectivity, embedding generation, synchronization, and application-level lookup remain unverified or pending.

### Table: canonical_concepts

```sql
CREATE TABLE schema_knowledge_base.canonical_concepts (
    id                  VARCHAR(100) PRIMARY KEY,  -- e.g. PART_TILE_WALL_PORCELAIN
    owner_tenant_id     UUID,                      -- Optional provenance only
    owner_user_id       UUID,                      -- Optional provenance only
    category            VARCHAR(100) NOT NULL,
    subcategory         VARCHAR(100),
    default_unit        VARCHAR(20) NOT NULL,
    apu_template_count  INTEGER NOT NULL DEFAULT 0,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by          UUID,
    updated_by          UUID,
    deleted_at          TIMESTAMPTZ,
    is_deleted          BOOLEAN NOT NULL DEFAULT FALSE,
    CONSTRAINT ck_canonical_concepts_id CHECK (id ~ '^[A-Z0-9_]{1,100}$'),
    CONSTRAINT ck_canonical_concepts_category CHECK (length(btrim(category)) > 0),
    CONSTRAINT ck_canonical_concepts_unit CHECK (default_unit ~ '^[A-Z0-9]+(/[A-Z0-9]+)?$'),
    CONSTRAINT ck_canonical_concepts_count CHECK (apu_template_count >= 0),
    CONSTRAINT ck_canonical_concepts_deleted CHECK (is_deleted = (deleted_at IS NOT NULL)),
    CONSTRAINT ck_canonical_concepts_owner_tenant CHECK (owner_tenant_id IS NULL OR owner_tenant_id <> '00000000-0000-0000-0000-000000000000'::uuid),
    CONSTRAINT ck_canonical_concepts_owner_user CHECK (owner_user_id IS NULL OR owner_user_id <> '00000000-0000-0000-0000-000000000000'::uuid)
);

CREATE TABLE schema_knowledge_base.concept_translations (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    concept_id          VARCHAR(100) NOT NULL REFERENCES schema_knowledge_base.canonical_concepts(id) ON DELETE CASCADE,
    language_code       VARCHAR(5) NOT NULL,  -- ES|EN|FR|PT|DE
    name                VARCHAR(300) NOT NULL,
    description         TEXT,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_concept_translations_language CHECK (language_code ~ '^[A-Z]{2}(-[A-Z]{2})?$'),
    CONSTRAINT ck_concept_translations_name CHECK (length(btrim(name)) > 0)
);
CREATE UNIQUE INDEX idx_concept_translations_unique ON schema_knowledge_base.concept_translations(concept_id, language_code);
```

`concept_translations` is an owned collection: its row ID is a persistence-only key, its parent controls lifecycle, and `(concept_id, language_code)` is unique. Direct reads of dependent rows must not bypass the parent's soft-delete filter.

### Table: apu_templates

```sql
CREATE TABLE schema_knowledge_base.apu_templates (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    owner_tenant_id     UUID,                      -- Optional provenance only
    owner_user_id       UUID,                      -- Optional provenance only
    canonical_concept_id VARCHAR(100) NOT NULL REFERENCES schema_knowledge_base.canonical_concepts(id) ON DELETE RESTRICT,
    description         VARCHAR(500) NOT NULL,
    project_type        VARCHAR(30) NOT NULL,      -- Residential|Commercial|Industrial
    geo_region          VARCHAR(10) NOT NULL,      -- ES-VC|ES-CT|ES-MD|...
    unit                VARCHAR(20) NOT NULL,
    yield_hours_per_unit DECIMAL(8,4) NOT NULL,
    crew_description    VARCHAR(200),
    usage_count         INTEGER NOT NULL DEFAULT 0,
    average_confidence  DECIMAL(4,3),
    source              VARCHAR(30) NOT NULL DEFAULT 'GeneratedByAI',
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by          UUID,
    updated_by          UUID,
    deleted_at          TIMESTAMPTZ,
    is_deleted          BOOLEAN NOT NULL DEFAULT FALSE,
    CONSTRAINT ck_apu_templates_description CHECK (length(btrim(description)) > 0),
    CONSTRAINT ck_apu_templates_project_type CHECK (project_type IN ('Residential','Commercial','Industrial','Infrastructure')),
    CONSTRAINT ck_apu_templates_region CHECK (geo_region ~ '^[A-Z]{2}-[A-Z0-9]{1,7}$'),
    CONSTRAINT ck_apu_templates_unit CHECK (unit ~ '^[A-Z0-9]+(/[A-Z0-9]+)?$'),
    CONSTRAINT ck_apu_templates_yield CHECK (yield_hours_per_unit > 0),
    CONSTRAINT ck_apu_templates_usage CHECK (usage_count >= 0 AND ((usage_count = 0 AND average_confidence IS NULL) OR (usage_count > 0 AND average_confidence BETWEEN 0 AND 1))),
    CONSTRAINT ck_apu_templates_source CHECK (source IN ('GeneratedByAI','ValidatedByHuman','ImportedFromExternal')),
    CONSTRAINT ck_apu_templates_deleted CHECK (is_deleted = (deleted_at IS NOT NULL)),
    CONSTRAINT ck_apu_templates_owner_tenant CHECK (owner_tenant_id IS NULL OR owner_tenant_id <> '00000000-0000-0000-0000-000000000000'::uuid),
    CONSTRAINT ck_apu_templates_owner_user CHECK (owner_user_id IS NULL OR owner_user_id <> '00000000-0000-0000-0000-000000000000'::uuid)
);
CREATE INDEX idx_apu_templates_concept ON schema_knowledge_base.apu_templates(canonical_concept_id, geo_region) WHERE is_deleted = FALSE;

CREATE TABLE schema_knowledge_base.apu_template_components (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    apu_template_id     UUID NOT NULL REFERENCES schema_knowledge_base.apu_templates(id) ON DELETE CASCADE,
    description         VARCHAR(300) NOT NULL,
    component_type      VARCHAR(20) NOT NULL,  -- Material|Labor|Equipment|Subcontract|Transport
    unit                VARCHAR(20) NOT NULL,
    yield               DECIMAL(10,6) NOT NULL,  -- Quantity per unit of work item
    sort_order          INTEGER NOT NULL,
    CONSTRAINT ck_apu_components_description CHECK (length(btrim(description)) > 0),
    CONSTRAINT ck_apu_components_type CHECK (component_type IN ('Material','Labor','Equipment','Subcontract','Transport')),
    CONSTRAINT ck_apu_components_unit CHECK (unit ~ '^[A-Z0-9]+(/[A-Z0-9]+)?$'),
    CONSTRAINT ck_apu_components_yield CHECK (yield > 0),
    CONSTRAINT ck_apu_components_sort CHECK (sort_order >= 0),
    CONSTRAINT uq_apu_components_position UNIQUE (apu_template_id, sort_order)
    -- NOTE: NO price column here. Prices are determined per-project in Phase 6.
);
```

`apu_template_components` is an owned collection. The aggregate assigns and restores its zero-based `sort_order`; EF persists that value and the unique `(apu_template_id, sort_order)` constraint prevents ambiguous ordering. Its FK cascades only when the parent is physically erased; normal removal remains soft delete. Component rows inherit visibility and lifecycle from their parent, and contain no prices. A `CanonicalConcept` counter update and creation of its `APUTemplate` must be coordinated transactionally when application/persistence is implemented.

---

## 5. schema_mcp — Provider Network {#s5}

The following is a conceptual schema description, not executable migration SQL. The current schema is defined by `McpDbContext`, its EF configurations, and the checked-in migrations. `McpProvider` is tenant-owned; server credentials are represented by a secret reference, not stored provider API-key hashes. The initial migration and optional-project migration were applied only to ephemeral PostgreSQL 17 according to governance; do not infer deployment to existing environments.

### Table: mcp_providers

```sql
CREATE TABLE schema_mcp.mcp_providers (
    id                  UUID PRIMARY KEY,
    tenant_id           UUID NOT NULL,
    name                VARCHAR(200) NOT NULL,
    categories          TEXT[] NOT NULL,
    geo_regions         TEXT[] NOT NULL,
    status              VARCHAR(20) NOT NULL,
    rating              DECIMAL(3,2) NOT NULL,
    consecutive_failures INTEGER NOT NULL DEFAULT 0,
    last_success_at     TIMESTAMPTZ,
    suspended_until     TIMESTAMPTZ,
    created_at          TIMESTAMPTZ NOT NULL,
    updated_at          TIMESTAMPTZ NOT NULL,
    created_by          UUID,
    updated_by          UUID,
    deleted_at          TIMESTAMPTZ,
    is_deleted          BOOLEAN NOT NULL,
    xmin                xid
);
CREATE INDEX idx_mcp_providers_region ON schema_mcp.mcp_providers USING GIN(geo_regions);
CREATE INDEX idx_mcp_providers_category ON schema_mcp.mcp_providers USING GIN(categories);
CREATE INDEX idx_mcp_providers_tenant_status ON schema_mcp.mcp_providers(tenant_id, status);
```

### Table: mcp_servers

```sql
CREATE TABLE schema_mcp.mcp_servers (
    id                          UUID PRIMARY KEY,
    tenant_id                   UUID NOT NULL,
    provider_id                 UUID NOT NULL,
    endpoint                    VARCHAR(500) NOT NULL,
    credential_secret_reference VARCHAR(200) NOT NULL,
    created_at                  TIMESTAMPTZ NOT NULL,
    updated_at                  TIMESTAMPTZ NOT NULL,
    created_by                  UUID,
    updated_by                  UUID,
    deleted_at                  TIMESTAMPTZ,
    is_deleted                  BOOLEAN NOT NULL,
    FOREIGN KEY (tenant_id, provider_id)
        REFERENCES schema_mcp.mcp_providers(tenant_id, id)
);
CREATE UNIQUE INDEX idx_mcp_servers_endpoint
    ON schema_mcp.mcp_servers(endpoint) WHERE is_deleted = FALSE;
CREATE INDEX idx_mcp_servers_provider ON schema_mcp.mcp_servers(tenant_id, provider_id);
```

### Table: mcp_query_logs

```sql
CREATE TABLE schema_mcp.mcp_query_logs (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id           UUID NOT NULL,
    project_id          UUID,
    provider_id         UUID NOT NULL REFERENCES schema_mcp.mcp_providers(id),
    canonical_concept_id VARCHAR(100) NOT NULL,
    component_type      VARCHAR(20) NOT NULL,
    quantity            DECIMAL(12,4) NOT NULL,
    unit                VARCHAR(20) NOT NULL,
    response_price      DECIMAL(12,4),
    response_unit       VARCHAR(20),
    fallback_source     VARCHAR(20),  -- Cache|Internet|AI; FallbackActivated is derived
    confidence          DECIMAL(4,3),
    duration_ms         INTEGER,
    status              VARCHAR(20) NOT NULL,  -- Success|Timeout|Error|Fallback
    credits_charged     DECIMAL(10,4) NOT NULL,
    queried_at          TIMESTAMPTZ NOT NULL,
    created_at          TIMESTAMPTZ NOT NULL,
    updated_at          TIMESTAMPTZ NOT NULL,
    created_by          UUID,
    updated_by          UUID,
    deleted_at          TIMESTAMPTZ,
    is_deleted          BOOLEAN NOT NULL
);
CREATE INDEX idx_mcp_query_logs_project ON schema_mcp.mcp_query_logs(tenant_id, project_id, queried_at);
CREATE INDEX idx_mcp_query_logs_provider ON schema_mcp.mcp_query_logs(tenant_id, provider_id, queried_at);
```

---

## 6. schema_ai — Agent Audit Trail {#s6}

The following describes the current EF mapping as a schema specification, not an applied migration or deployed database. `AiDbContext` maps `schema_ai.agent_runs`, applies tenant and soft-delete filters, and configures `xmin` concurrency. The AI module has no migration in the repository; persistence round-trip and migration application are not verified. `credits_charged` records a value but does not demonstrate billing integration.

### Table: agent_runs

```sql
CREATE TABLE schema_ai.agent_runs (
    id                  UUID PRIMARY KEY,
    tenant_id           UUID NOT NULL,
    project_id          UUID,
    agent_type          VARCHAR(30) NOT NULL,  -- Capture|Scope|Production|Planning|Valuation|Offer|Conversation|Embedding
    model_id            VARCHAR(50) NOT NULL,  -- deepseek-v3|gemini-flash|gemma4|...
    provider            VARCHAR(30) NOT NULL,  -- DeepSeek|Gemini|Gemma|OpenAI
    input_tokens        INTEGER NOT NULL DEFAULT 0,
    output_tokens       INTEGER NOT NULL DEFAULT 0,
    credits_charged     DECIMAL(10,4) NOT NULL DEFAULT 0,
    fallback_activated  BOOLEAN NOT NULL DEFAULT FALSE,
    fallback_reason     VARCHAR(200),
    status              VARCHAR(20) NOT NULL,  -- Queued|Success|Failed|FallbackUsed
    duration_ms         INTEGER NOT NULL,
    human_reviewed      BOOLEAN NOT NULL DEFAULT FALSE,  -- AI Act Art. 14
    human_reviewed_at   TIMESTAMPTZ,
    human_reviewer_id   UUID,
    created_at          TIMESTAMPTZ NOT NULL,
    updated_at          TIMESTAMPTZ NOT NULL,
    created_by          UUID,
    updated_by          UUID,
    deleted_at          TIMESTAMPTZ,
    is_deleted          BOOLEAN NOT NULL,
    xmin                xid
);
CREATE INDEX idx_agent_runs_project ON schema_ai.agent_runs(project_id, created_at);
CREATE INDEX idx_agent_runs_tenant ON schema_ai.agent_runs(tenant_id, created_at);
```

---

## 7. schema_bots — Conversational Channels {#s7}

Implemented by `BotsDbContext` and migration `20261001231739_InitialBotsSchema`; validated only in ephemeral PostgreSQL 17. The SQL below describes the mapped schema, not a deployment script. IDs and audit timestamps are supplied by the domain. Both entities inherit `BaseEntity<TId>` (the conversation through `AggregateRoot<TId>`). PostgreSQL's system column `xmin` is the conversation concurrency token; it is not an application-created column.

### Table: bot_conversations

```sql
CREATE TABLE schema_bots.bot_conversations (
    id                  UUID PRIMARY KEY,
    tenant_id           UUID NOT NULL,
    external_id         VARCHAR(100) NOT NULL,  -- Phone (WA) or ChatId (TG)
    channel             VARCHAR(20) NOT NULL,   -- Telegram|WhatsApp
    user_id             UUID NOT NULL,          -- Supplied by caller after external authentication; no Identity FK
    active_project_id   UUID,                   -- SPA: System of Project Active
    state               VARCHAR(30) NOT NULL DEFAULT 'NewSession',
    verbosity           VARCHAR(20) NOT NULL DEFAULT 'Normal',
    last_interaction_at TIMESTAMPTZ NOT NULL,
    created_at          TIMESTAMPTZ NOT NULL,
    updated_at          TIMESTAMPTZ NOT NULL,
    created_by          UUID,
    updated_by          UUID,
    is_deleted          BOOLEAN NOT NULL,
    deleted_at          TIMESTAMPTZ,
    CONSTRAINT ak_bot_conversations_tenant_id_id UNIQUE (tenant_id, id),
    CONSTRAINT ck_bot_conversations_deleted CHECK (is_deleted = (deleted_at IS NOT NULL))
);
CREATE UNIQUE INDEX idx_bot_conversations_external ON schema_bots.bot_conversations(tenant_id, channel, external_id) WHERE is_deleted = FALSE;
```

### Table: bot_messages

```sql
CREATE TABLE schema_bots.bot_messages (
    id                  UUID PRIMARY KEY,
    conversation_id     UUID NOT NULL,
    tenant_id           UUID NOT NULL,
    direction           VARCHAR(10) NOT NULL,  -- Inbound|Outbound
    message_type        VARCHAR(20) NOT NULL,  -- Text|Image|Audio|Video|Document|Command
    content             TEXT,
    media_url           VARCHAR(500),
    parsed_command      VARCHAR(100),
    created_at          TIMESTAMPTZ NOT NULL,
    updated_at          TIMESTAMPTZ NOT NULL,
    created_by          UUID,
    updated_by          UUID,
    is_deleted          BOOLEAN NOT NULL,
    deleted_at          TIMESTAMPTZ,
    CONSTRAINT ck_bot_messages_deleted CHECK (is_deleted = (deleted_at IS NOT NULL)),
    CONSTRAINT ck_bot_messages_content CHECK (content IS NOT NULL OR media_url IS NOT NULL),
    CONSTRAINT ck_bot_messages_media_url_https CHECK (media_url IS NULL OR (media_url LIKE 'https://%' AND length(media_url) <= 500)),
    CONSTRAINT "FK_bot_messages_bot_conversations_tenant_id_conversation_id"
        FOREIGN KEY (tenant_id, conversation_id) REFERENCES schema_bots.bot_conversations(tenant_id, id) ON DELETE RESTRICT
);
CREATE INDEX idx_bot_messages_conversation ON schema_bots.bot_messages(tenant_id, conversation_id, created_at);
```

EF query filters restrict both tables to the current tenant and non-deleted rows; message history also excludes deleted conversations. There are no cross-module foreign keys to Identity or Projects. User authentication, tenant membership and project authorization remain responsibilities of the pending application/channel integration. See [verified scope and evidence](../Gobernanza/Estado-y-gobernanza.md#frontera-de-dominio-datos-y-persistencia-de-bots).

---

## 8. schema_billing — Credit Economy {#s8}

The current Billing mapping is described below from its EF configurations and checked-in initial migration. The migration was tested only in ephemeral PostgreSQL 17 according to governance; this specification does not claim deployment to existing databases. Current local Billing scope excludes Stripe webhook processing and durable Identity-to-Billing integration.

### Table: credit_accounts

```sql
CREATE TABLE schema_billing.credit_accounts (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL UNIQUE,
    plan_id VARCHAR(100) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL,
    created_by UUID,
    updated_by UUID,
    is_deleted BOOLEAN NOT NULL,
    deleted_at TIMESTAMPTZ,
    xmin xid,
    CHECK (is_deleted = (deleted_at IS NOT NULL))
);
```

`AvailableCredits` and `ReservedCredits` are calculated from lots and are not persisted on the account.

### Table: credit_lots

```sql
CREATE TABLE schema_billing.credit_lots (
    id UUID PRIMARY KEY,
    amount NUMERIC(18,4) NOT NULL,
    available_credits NUMERIC(18,4) NOT NULL,
    reserved_credits NUMERIC(18,4) NOT NULL,
    source VARCHAR(30) NOT NULL,
    purchased_at TIMESTAMPTZ NOT NULL,
    expires_at TIMESTAMPTZ NOT NULL,
    tenant_id UUID NOT NULL REFERENCES schema_billing.credit_accounts(tenant_id),
    created_at TIMESTAMPTZ NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL,
    created_by UUID,
    updated_by UUID,
    is_deleted BOOLEAN NOT NULL,
    deleted_at TIMESTAMPTZ,
    CHECK (amount >= 0 AND available_credits >= 0 AND reserved_credits >= 0
           AND amount = available_credits + reserved_credits),
    CHECK (expires_at >= purchased_at + interval '3 months'),
    CHECK (is_deleted = (deleted_at IS NOT NULL))
);
CREATE INDEX ix_billing_credit_lots_tenant_expiry
    ON schema_billing.credit_lots(tenant_id, expires_at, purchased_at);
```

The mapped `amount` column stores `CreditLot.RemainingCredits`. Current constraints enforce a non-negative balance, `amount = available_credits + reserved_credits`, and at least three months between purchase and expiry.

### Table: credit_transactions

```sql
CREATE TABLE schema_billing.credit_transactions (
    id UUID PRIMARY KEY,
    type VARCHAR(30) NOT NULL,
    amount NUMERIC(18,4) NOT NULL,
    operation_id UUID,
    source VARCHAR(30),
    expires_at TIMESTAMPTZ,
    tenant_id UUID NOT NULL REFERENCES schema_billing.credit_accounts(tenant_id),
    created_at TIMESTAMPTZ NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL,
    created_by UUID,
    updated_by UUID,
    is_deleted BOOLEAN NOT NULL,
    deleted_at TIMESTAMPTZ,
    CHECK (amount > 0),
    CHECK (type = 'Recharged' OR operation_id IS NOT NULL),
    CHECK (is_deleted = (deleted_at IS NOT NULL))
);
CREATE INDEX ix_billing_credit_transactions_history
    ON schema_billing.credit_transactions(tenant_id, created_at, id);
CREATE INDEX ix_billing_credit_transactions_operation
    ON schema_billing.credit_transactions(tenant_id, operation_id, created_at);
```

Transactions own allocation rows in `credit_transaction_allocations`; EF maps this as an owned collection. The migration gives each row an integer identity primary key and a cascading FK to its transaction. It stores `credit_lot_id` as a value, without a database FK to `credit_lots`.

```sql
CREATE TABLE schema_billing.credit_transaction_allocations (
    id INTEGER GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    credit_lot_id UUID NOT NULL,
    amount NUMERIC(18,4) NOT NULL,
    credit_transaction_id UUID NOT NULL REFERENCES schema_billing.credit_transactions(id) ON DELETE CASCADE
);
```

The current schema does not have `credit_account_id`, `description`, or a stored account balance.

### Table: subscriptions

```sql
CREATE TABLE schema_billing.subscriptions (
    id UUID PRIMARY KEY,
    plan_id VARCHAR(100) NOT NULL,
    status VARCHAR(30) NOT NULL,
    cycle VARCHAR(20) NOT NULL,
    current_period_start TIMESTAMPTZ NOT NULL,
    current_period_end TIMESTAMPTZ NOT NULL,
    stripe_subscription_id VARCHAR(100),
    credits_per_cycle INTEGER NOT NULL,
    tenant_id UUID NOT NULL UNIQUE REFERENCES schema_billing.credit_accounts(tenant_id),
    created_at TIMESTAMPTZ NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL,
    created_by UUID,
    updated_by UUID,
    is_deleted BOOLEAN NOT NULL,
    deleted_at TIMESTAMPTZ,
    CHECK (current_period_end > current_period_start),
    CHECK (credits_per_cycle > 0),
    CHECK (is_deleted = (deleted_at IS NOT NULL))
);
CREATE UNIQUE INDEX ux_billing_subscriptions_stripe_id
    ON schema_billing.subscriptions(stripe_subscription_id)
    WHERE stripe_subscription_id IS NOT NULL;
```

Stripe event inbox/webhook tables, `stripe_customer_id`, and provider-driven purchases are not part of the current Billing EF model or migration.

---

## 9. schema_identity — Multi-Tenant Auth {#s9}

The following is a schema specification derived from the Identity EF configurations and checked-in initial migration. The migration exists on disk; the recorded PostgreSQL round-trip uses `EnsureCreated`, not `MigrateAsync`. The migration has not been applied to persistent databases.

### Table: tenants

```sql
CREATE TABLE schema_identity.tenants (
    id UUID PRIMARY KEY,
    name VARCHAR(200) NOT NULL,
    slug VARCHAR(63) NOT NULL,
    type VARCHAR(30) NOT NULL,
    tax_id VARCHAR(32),
    tax_country VARCHAR(2),
    fiscal_country VARCHAR(2),
    fiscal_region VARCHAR(100),
    fiscal_province VARCHAR(100),
    fiscal_municipality VARCHAR(100),
    fiscal_postal_code VARCHAR(20),
    fiscal_street VARCHAR(300),
    plan_id VARCHAR(100) NOT NULL,
    status VARCHAR(30) NOT NULL,
    branding_company_name VARCHAR(200),
    branding_primary_color VARCHAR(7),
    branding_logo_url VARCHAR(2048),
    branding_email VARCHAR(320),
    settings_administration NUMERIC(5,2) NOT NULL,
    settings_profit NUMERIC(5,2) NOT NULL,
    settings_quality NUMERIC(5,2) NOT NULL,
    settings_safety_health NUMERIC(5,2) NOT NULL,
    settings_environment NUMERIC(5,2) NOT NULL,
    settings_contingency NUMERIC(5,2) NOT NULL,
    tenant_id UUID NOT NULL,
    created_at TIMESTAMPTZ NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL,
    created_by UUID,
    updated_by UUID,
    is_deleted BOOLEAN NOT NULL,
    deleted_at TIMESTAMPTZ,
    xmin XID NOT NULL,
    UNIQUE (tenant_id, id),
    CHECK (tenant_id = id),
    CHECK (is_deleted = (deleted_at IS NOT NULL))
);
CREATE UNIQUE INDEX ux_identity_tenants_slug
    ON schema_identity.tenants(slug) WHERE is_deleted = FALSE;
```

### Table: users

```sql
CREATE TABLE schema_identity.users (
    id UUID PRIMARY KEY,
    first_name VARCHAR(100) NOT NULL,
    last_name VARCHAR(100) NOT NULL,
    normalized_email VARCHAR(320) NOT NULL,
    phone_number VARCHAR(16),
    password_hash VARCHAR(1024),
    status VARCHAR(30) NOT NULL,
    tenant_id UUID NOT NULL,
    created_at TIMESTAMPTZ NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL,
    created_by UUID,
    updated_by UUID,
    is_deleted BOOLEAN NOT NULL,
    deleted_at TIMESTAMPTZ,
    xmin XID NOT NULL,
    UNIQUE (tenant_id, id),
    CHECK (is_deleted = (deleted_at IS NOT NULL))
);
CREATE UNIQUE INDEX ux_identity_users_normalized_email
    ON schema_identity.users(normalized_email) WHERE is_deleted = FALSE;
CREATE INDEX ix_identity_users_tenant_status
    ON schema_identity.users(tenant_id, status);
```

### Table: tenant_users

```sql
CREATE TABLE schema_identity.tenant_users (
    id UUID PRIMARY KEY,
    user_id UUID NOT NULL,
    role VARCHAR(30) NOT NULL,
    tenant_id UUID NOT NULL,
    created_at TIMESTAMPTZ NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL,
    created_by UUID,
    updated_by UUID,
    is_deleted BOOLEAN NOT NULL,
    deleted_at TIMESTAMPTZ,
    xmin XID NOT NULL,
    FOREIGN KEY (tenant_id, user_id)
        REFERENCES schema_identity.users(tenant_id, id) ON DELETE RESTRICT,
    CHECK (is_deleted = (deleted_at IS NOT NULL))
);
CREATE INDEX ix_identity_tenant_users_tenant_role
    ON schema_identity.tenant_users(tenant_id, role);
CREATE INDEX IX_tenant_users_tenant_id_user_id
    ON schema_identity.tenant_users(tenant_id, user_id);
CREATE UNIQUE INDEX ux_identity_tenant_users_user
    ON schema_identity.tenant_users(user_id);
```

### Table: refresh_tokens

```sql
CREATE TABLE schema_identity.refresh_tokens (
    id UUID PRIMARY KEY,
    user_id UUID NOT NULL,
    token_hash VARCHAR(64) NOT NULL,
    expires_at TIMESTAMPTZ NOT NULL,
    revoked_at TIMESTAMPTZ,
    replaced_by_token_id UUID,
    tenant_id UUID NOT NULL,
    created_at TIMESTAMPTZ NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL,
    created_by UUID,
    updated_by UUID,
    is_deleted BOOLEAN NOT NULL,
    deleted_at TIMESTAMPTZ,
    xmin XID NOT NULL,
    CHECK (is_deleted = (deleted_at IS NOT NULL))
);
CREATE UNIQUE INDEX ux_identity_refresh_tokens_hash
    ON schema_identity.refresh_tokens(token_hash);
CREATE INDEX ix_identity_refresh_tokens_owner_expiry
    ON schema_identity.refresh_tokens(tenant_id, user_id, expires_at);
```

### Table: user_invitations

```sql
CREATE TABLE schema_identity.user_invitations (
    id UUID PRIMARY KEY,
    user_id UUID NOT NULL,
    token_hash VARCHAR(64) NOT NULL,
    expires_at TIMESTAMPTZ NOT NULL,
    accepted_at TIMESTAMPTZ,
    revoked_at TIMESTAMPTZ,
    tenant_id UUID NOT NULL,
    created_at TIMESTAMPTZ NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL,
    created_by UUID,
    updated_by UUID,
    is_deleted BOOLEAN NOT NULL,
    deleted_at TIMESTAMPTZ,
    xmin XID NOT NULL,
    CHECK (is_deleted = (deleted_at IS NOT NULL))
);
CREATE UNIQUE INDEX ux_identity_invitations_hash
    ON schema_identity.user_invitations(token_hash);
CREATE INDEX ix_identity_invitations_owner_expiry
    ON schema_identity.user_invitations(tenant_id, user_id, expires_at);
```

The current schema stores branding and company settings in flattened columns, not JSONB. `normalized_email` is the unique email column; user channel links, tenant defaults, and JSON address/branding fields from the former design are not mapped. Refresh tokens and invitations store hashes; their current EF mappings do not define foreign keys to users.

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

The KnowledgeBase Qdrant adapter currently exposes vector-index primitives for canonical concepts and APU templates. It uses 768-dimensional vectors with cosine distance and collection names configurable through `KnowledgeBaseVectorOptions` (defaults: `canonical_concepts` and `apu_structures`). The adapter receives embeddings as input; generation by a particular AI provider and reliable synchronization with PostgreSQL are not implemented by this adapter.

### Current adapter payload: `canonical_concepts`

The adapter currently writes only `entity_id`. Search results return this entity identifier and the Qdrant score.

```json
{ "entity_id": "canonical concept identifier" }
```

### Current adapter payload: `apu_structures`

The adapter currently writes these payload fields. Search results return `entity_id` and the Qdrant score.

```json
{
  "entity_id": "APU template Guid in D format",
  "canonical_concept_id": "canonical concept identifier",
  "project_type": "project type",
  "region": "region"
}
```

Point IDs are stable identifiers derived by the adapter. Metadata previously listed for categories, translations, units, component types, usage statistics and sources is not currently written by this adapter and is not its persisted payload contract.

### Planned collection: `project_contexts`

Project-context indexing and search are not implemented by the current adapter. Treat the proposed collection and payload fields as design only until implementation and verification are recorded.

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

Index targets depend on ownership and query patterns; global catalog tables do not contain `tenant_id`.

| Index Type | Columns | Applied to |
|---|---|---|
| Tenant-owned entities | (tenant_id) WHERE is_deleted = FALSE | Tenant-owned tables when supported by their query patterns |
| Time-based queries | (created_at DESC) | Tables queried by creation time |
| Foreign key | (project_id) | work_items, apu_assignments, schedules, valuations, reviews, offers |
| Unique, partial | (normalized_email) WHERE is_deleted = FALSE | schema_identity.users |
| Unique, partial | (slug) WHERE is_deleted = FALSE | schema_identity.tenants |
| Unique, partial | (endpoint) WHERE is_deleted = FALSE | schema_mcp.mcp_servers |
| GIN (array) | (geo_regions), (categories) | schema_mcp.mcp_providers |
| GIN (JSONB) | (components) | schema_projects.apu_assignments (if queried) |
| Partial | (canonical_concept_id, geo_region) | schema_knowledge_base.apu_templates |

> **Performance Rule:** Never add indexes speculatively. Add indexes only when a slow query is identified via OpenTelemetry traces or EXPLAIN ANALYZE. Over-indexing degrades write performance.

---

*KYNAKEE PLATFORM · Database Design v1.0 · 2026-08-20*  
*Confidential · For internal development use only*
