# System Design – PersonalizedFeed

This document describes the design of the **PersonalizedFeed** backend:

* Architecture and data flows
* Data model for user signals and content
* API contract and error semantics
* CMS configuration surface
* Trade-offs and decisions
* Maturity policy handling
* Rollout, observability, and “with more time” roadmap

Implementation is in **.NET 8**, using plain **ASP.NET Core + Worker services**, orchestrated by **.NET Aspire** for multi-service runs and OpenTelemetry defaults.

---

## 1. Architecture overview

graph TD
    Client[Mobile SDK]
    LB[Load Balancer]
    API[Feed API .NET 8]
    Worker[Events Worker]
    
    subgraph Data Stores
        Redis[(Azure Redis)]
        SQL[(Azure SQL)]
        ServiceBus{Azure Service Bus}
        Blob[Blob Storage]
    end

    %% Read Path
    Client -- GET /feed --> LB --> API
    API -- 1. Check Config --> Redis
    API -- 2. Get Candidates --> Redis
    API -- 3. Get User Signals --> Redis
    
    %% Write Path
    Client -- POST /events --> LB --> API
    API -- Async Batch --> ServiceBus
    ServiceBus -- Dequeue --> Worker
    Worker -- Atomic Update --> SQL
    Worker -- Update Cache --> Redis
    Worker -- Archive Raw --> Blob

### 1.1 Core components

**Mobile SDK**

* Calls:

  * `GET /v1/feed` – fetch personalized vertical video feed.
  * `POST /v1/events/batch` – send user interactions (views, likes, skips, etc.).

**Feed API – `PersonalizedFeed.Api`**

* Validates tenant + API key + hashed user ID headers.
* Loads `TenantConfig` and `UserSignals`.
* Fetches candidate videos with lightweight retrieval + maturity filtering.
  * We choose that the globally popular tenant-specific set of 500 or fewer videos are fetched here.
* Runs ranking pipeline (feature extraction → model → diversification).
* Returns ranked feed with mode `personalized` or `fallback`.
* Accepts batched user events and forwards them into the ingestion pipeline via `IUserEventSink`:

  * local demo: inline ingestion,
  * production: Azure Service Bus.

**Events Worker – `PersonalizedFeed.Worker`**

* Subscribes to a `user-events` queue (Azure Service Bus in production).
* Deserializes `UserEventBatch` messages.
* Calls `IUserEventIngestionService` to:

  * update `UserSignals`,
  * update `VideoStats`,
  * optionally append raw events to blob storage.

  * Updates use optimistic concurrency (SQL) or atomic increments (Redis) to handle high-concurrency race conditions.

**Data stores (production design)**

* **Azure SQL**

  * `TenantConfigs` – per-tenant configuration and ranking weights.
  * `Videos` – content metadata (title, tags, maturity, editorial boost).
  * `UserSignals` – user-level aggregates driving personalization.
  * `VideoStats` – content-level aggregates for popularity.
  * `SystemConfig` – global flags (e.g., personalization kill switch).
* **Azure Cache for Redis**

  * Hot cache for:

    * `TenantConfigs` (keyed by `TenantId`),
    * `UserSignals` (keyed by `TenantId + UserHash`).
* **Azure Service Bus**

  * Queue `user-events` for `UserEventBatch` messages.
* **Azure Blob Storage**

  * Container `userevents-raw` storing raw events with a 90-day lifecycle.

**CMS**

* Separate system managing:

  * Content in `Videos`
  * Per-tenant configuration in `TenantConfigs`.

**Prototype**

* All of the above are represented by **in-memory repositories** so the API and Worker run locally with **no Azure or Docker dependencies**.

---

### 1.2 Network / VNet & privacy

Production deployment assumes:

* **API + Worker** run in **App Service / Container Apps** integrated into an Azure **VNet**.
* **Azure SQL, Redis, Service Bus, Blob** are reachable only via **private endpoints** inside that VNet.
* SDK sends **hashed user IDs** (no raw PII).
* No user-level identifiers are sent to external services (ML, LLMs, etc.); external AI, if used, operates only on content metadata offline.

This aligns with the requirement that **no raw PII leaves the private network** and user-level personalization lives entirely inside the VNet.

---

### 1.3 Where ranking happens

Ranking is encapsulated in the **domain layer**:

* `IFeatureExtractor` → builds `RankingFeatures` for `(tenant, user, video)`.
* `IRankingModel` → computes scalar score from `RankingFeatures` and a `RankingModelDefinition` (e.g. linear model config).
* `IFeedDiversifier` → post-processes scored items to:

  * avoid near-duplicate titles,
  * cap streaks of same `MainTag`.
* `IRanker` → orchestrates feature extraction, scoring, and diversification.

`FeedService` uses these components to implement the read path:

* **Personalized mode**:

  * uses `UserSignals` to compute category affinity, watch-time features, skip-rate penalties.
* **Fallback mode**:

  * ignores `UserSignals` and uses popularity + editorial features only.

`FeedService` is then consumed by the HTTP endpoints in `PersonalizedFeed.Api`.

---

### 1.4 Caching strategy

#### 1.4.1 Data-level caching (Redis)

Repository interfaces allow caching to be added without touching domain code:

* `ITenantConfigRepository`
* `IUserSignalsRepository`

Intended production behavior:

* **Read path**

  * Try Redis first.
  * On miss, load from Azure SQL and populate Redis.
* **Write path**

  * Worker writes to SQL.
  * Then updates / invalidates Redis entries for affected tenant/user.

Prototype:

* Uses in-memory dictionaries behind these interfaces.
* Swapping to SQL+Redis is primarily an infrastructure concern.

#### 1.4.2 HTTP-level caching

* `GET /v1/feed` is **not cacheable** in v1:

  * `Cache-Control: no-store`.
* Rationale:

  * Feeds depend on user signals, ranking weights, feature flags, and fresh content;
  * avoiding complex cache invalidation for this prototype.

With more time, HTTP-level caching could be introduced for:

* Non-personalized feeds,
* Or very short-lived per-user caching during a session.

---

## 2. Data model

The data model is designed for **Azure SQL** but implemented as domain models + in-memory repositories in the prototype.

### 2.1 `TenantConfigs`

Per-tenant configuration and ranking model.

* **Key**: `TenantId` (PK)
* **Fields**:

  * `ApiKey` – tenant-level API key used by the SDK.
  * `UsePersonalization` – feature flag per tenant.
  * `DefaultLimit` – default feed size.
  * `MaturityPolicy` – e.g. `"PG13"`.
  * `RankingModelType` – e.g. `"linear"`.
  * `RankingModelVersion` – arbitrary string version.
  * `RankingModelPayloadJson` – JSON payload with weights and bias.
  * `FeatureFlagsJson` – arbitrary JSON for per-tenant experiments.

Prototype seeding uses:

* `TenantId = "tenant_1"`
* `ApiKey = "secret-api-key"`
* A simple linear model with high `CategoryAffinity` weight and recency penalty.

---

### 2.2 `Videos`

Content metadata owned by CMS.

* **Key**: `(TenantId, VideoId)`
* **Fields**:

  * `PlaybackUrl`, `ThumbnailUrl`
  * `Title`, `MainTag`, `Tags`
  * `DurationSeconds`
  * `MaturityRating` (e.g. `"G"`, `"PG"`, `"PG13"`, `"R"`)
  * `EditorialBoost`
  * `GlobalPopularityScore`
  * `CreatedAt`, `UpdatedAt`
  * `IsActive` (soft delete / hide)

Prototype seeds two videos for `tenant_1`:

* `vid_fitness_1` – `"Fitness warmup"`, `MainTag = "fitness"`.
* `vid_cooking_1` – `"Cooking pasta"`, `MainTag = "cooking"`.

---

### 2.3 `UserSignals`

Aggregated behaviour for a `(TenantId, UserHash)` pair.

* **Key**: `(TenantId, UserHash)`
* **Fields**:

  * `CategoryStats` – dictionary: `category -> {views, watchTimeMs, skips}`.
  * `TotalViewsLast7d`
  * `TotalWatchTimeLast7dMs`
  * `SkipRateLast7d`
  * `LastActiveAt`
  * `UpdatedAt`

Retention:

* Designed to reflect **behaviour over time windows** (e.g. last 7 days).
* Older behaviour is naturally aged out by recomputing these aggregates.
* Raw events backing these aggregates are retained in blob storage for **90 days**.

---

### 2.4 `VideoStats`

Aggregated stats per video.

* **Key**: `(TenantId, VideoId)`
* **Fields**:

  * `ViewsLast7d`
  * `LikesLast7d`
  * `AvgWatchTimeLast7dMs`
  * `UpdatedAt`

`VideoStats` can be used to derive or refresh `GlobalPopularityScore` in `Videos` periodically.

---

### 2.5 `SystemConfig`

Simple key-value configuration.

* **Key**: `Key` (`nvarchar`)
* **Value**: `Value` (`nvarchar`)

Example:

* `Key = "Personalization.Enabled"`, `Value = "true"`.

---

### 2.6 Raw events (Blob, not SQL)

Raw user-level events are written by the worker to:

* Container: `userevents-raw`
* Path prefix: `tenantId/yyyy/MM/dd/...`

with:

* **Retention**: automatic deletion after **90 days** using blob lifecycle rules.
* **Use cases**:

  * Training / evaluating ranking models.
  * Audits and debugging.

The prototype does not implement this write yet; it’s described as a clear next step.

---

## 3. API contract

### 3.1 `GET /v1/feed`

Fetch a personalized (or fallback) feed of video items.

**Headers**

* `X-Tenant-Id: <tenantId>`
* `X-Api-Key: <apiKey>`
* `X-User: <hashedUserId>`

**Query**

* `limit` – optional; default 20, max 50.
* `cursor` – optional; opaque paging token (v1 implementation is simple offset-based).

**Response 200**

```json
{
  "mode": "personalized",           // or "fallback"
  "items": [
    {
      "videoId": "vid_fitness_1",
      "playbackUrl": "https://cdn.example.com/v/vid_fitness.m3u8",
      "thumbnailUrl": null,
      "title": "Fitness warmup",
      "mainTag": "fitness",
      "tags": ["fitness"],
      "durationSeconds": 30,
      "maturityRating": "PG",
      "score": 12.34,
      "rank": 0
    }
  ],
  "nextCursor": "offset:20"
}
```

**Caching**

* `Cache-Control: no-store`
  Intentional trade-off for a highly personalised and dynamic feed.

**Error semantics**

* `400 Bad Request` – missing required headers, invalid query parameters.
* `401 Unauthorized` – invalid `X-Api-Key` for the given `X-Tenant-Id`.
* `404 Not Found` – used by debug endpoints (e.g. no `UserSignals` for requested user).
* `429 Too Many Requests` – reserved for rate limiting (not implemented in prototype).
* `500 Internal Server Error` – unexpected errors; response can include opaque `errorId` for correlation.

---

### 3.2 `POST /v1/events/batch`

Ingest batched user events from the SDK.

**Headers**

* `X-Tenant-Id: <tenantId>`
* `X-Api-Key: <apiKey>`
* `X-User: <hashedUserId>`

**Body**

```json
{
  "events": [
    {
      "type": "video_view",
      "videoId": "vid_fitness_1",
      "timestamp": "2025-12-01T12:00:00Z",
      "watchTimeMs": 18000,
      "feedRequestId": "req-1",
      "rankPosition": 0
    },
    {
      "type": "skip",
      "videoId": "vid_cooking_1",
      "timestamp": "2025-12-01T12:01:00Z",
      "watchTimeMs": null,
      "feedRequestId": "req-1",
      "rankPosition": 1
    }
  ]
}
```

**Response**

* `202 Accepted` – events accepted into the ingestion pipeline.

Prototype behavior:

* `InlineUserEventSink` processes events synchronously in the API process and updates `UserSignals` directly.

Production behavior:

* `ServiceBusUserEventSink` publishes `UserEventBatch` messages to the `user-events` queue.
* `PersonalizedFeed.Worker` consumes and processes them via `IUserEventIngestionService`.

---

### 3.3 Debug endpoints (prototype only)

To make the system easier to inspect in a code review:

* `GET /v1/debug/user-signals?userHash=...`

  * Returns `UserSignals` for a given `(tenant, userHash)`.
  * Used to verify that events are being ingested and aggregated.
* `POST /v1/debug/set-personalization?enable=...`

  * Toggles `TenantConfig.UsePersonalization` in the in-memory store.
  * Demonstrates per-tenant kill switch behaviour without a CMS UI.

These would be removed or admin-locked in production.

---

## 4. CMS configuration

The CMS (Content Management System) is where content managers configure the business rules that shape the personalized feed.
The demo uses in-memory data, but the architecture is explicitly **CMS-driven**: the CMS writes into the same configuration model that the API and Worker consume.

### 4.1 CMS → backend configuration model

The CMS interacts with the backend via structured configuration objects stored in per-tenant `TenantConfig` records and per-video `Video` records.

**TenantConfig-level settings:**

| Setting                   | Type   | Purpose                                            |
| ------------------------- | ------ | -------------------------------------------------- |
| `MaturityPolicy`          | string | Max allowed rating (e.g. `"PG"`, `"PG13"`, `"R"`). |
| `UsePersonalization`      | bool   | Turn personalization on/off per tenant.            |
| `RankingModelPayloadJson` | JSON   | Weights / bias used by ranking model.              |
| `RankingModelType`        | string | `"linear"`, `"ml-v1"`, `"bandit"`, etc.            |
| `FeatureFlagsJson`        | JSON   | Arbitrary flags to enable/disable experiments.     |
| `DefaultLimit`            | int    | Default page size for `/v1/feed`.                  |

**Video-level settings (managed by CMS):**

* `Title`, `PlaybackUrl`, `ThumbnailUrl`
* `MainTag`, `Tags`
* `MaturityRating`
* `EditorialBoost`
* `IsActive`

In production these are persisted in Azure SQL (or Cosmos DB); in the prototype they live in `InMemoryDataStore`.

---

### 4.2 CMS workflows (intended production behavior)

#### A. Manage video metadata

Content editors can:

* Upload or update video metadata.
* Set **maturity ratings**.
* Set **editorial boosts** for campaigns.
* Mark content active/inactive.
* Adjust how popularity is computed or overridden.

Backend mapping:

* Writes into `Videos`.
* Editorial boosts feed into `RankingFeatures` and thus into scoring.

#### B. Configure tenant rules

Per tenant, editors can:

1. **Set maturity policy**

   ```text
   MaturityPolicy = "PG"
   ```

   The maturity policy code ensures only content with `MaturityRating <= MaturityPolicy` is eligible.

2. **Tune ranking model weights**

   Stored as JSON, e.g.:

   ```json
   {
     "CategoryAffinity": 15.0,
     "RecencyHours": -0.1,
     "GlobalPopularityScore": 0.5,
     "EditorialBoost": 1.0,
     "UserWatchTimeLast7d": 0.0,
     "UserSkipRateLast7d": 0.0,
     "IsMatureContent": -100.0
   }
   ```

3. **Toggle personalization**

   ```text
   UsePersonalization = true/false
   ```

   Global override exists in `SystemConfig`.

4. **Set feature flags**

   ```json
   {
     "use-new-diversifier": true,
     "enable-threshold-decay": false
   }
   ```

   Used by API/Worker to change behaviour without redeployments.

---

### 4.3 CMS update flow

Typical production flow:

```text
CMS UI → Admin API → Azure SQL (TenantConfig, Videos) → Redis cache → FeedService / Worker
```

On updates:

* New config/model weights stored.
* Redis cache invalidated for affected tenant.
* Subsequent feed requests for that tenant use the new configuration.
* Maturity policy and editorial boosts take effect immediately once caches refresh.

---

### 4.4 How the prototype demonstrates CMS hooks

Even without a real CMS, the prototype shows CMS integration points:

* `TenantConfig.RankingModelPayloadJson` is deserialized into `LinearModelConfig` at runtime.
* `MaturityPolicy` is used by `IVideoRepository.GetCandidateVideosAsync` via a shared `MaturityRatingPolicy`.
* `UsePersonalization` and global `SystemConfig` toggles determine `personalized` vs `fallback` mode.
* Editorial boosts are part of the `Video` model and can influence ranking.
* Debug endpoints demonstrate how changing config affects behaviour immediately.

---

### 4.5 CMS enhancements with more time

With additional time, the CMS-facing part would gain:

* Full admin API for TenantConfig management.
* Versioned ranking models with rollback.
* A/B experiment allocation via CMS (traffic % to model variants).
* “Preview feed as user X” tooling.
* Bulk content import + scheduled editorial boosts.
* Validation rules to prevent misconfigurations that might expose adult content to child tenants.

---

## 5. Trade-offs & decisions

### In-memory infra vs real Azure resources

* **Decision:** use in-memory repositories for prototype.
* **Why:** make the solution `git clone → dotnet run` with no external setup.
* **Reversibility:** repository interfaces allow plugging in Azure SQL, Redis, Service Bus, and Blob with minimal domain changes.

### Inline ingestion vs Service Bus

* **Decision:** demo uses `InlineUserEventSink` (synchronous ingestion in API process).
* **Why:** easier to understand and test; `/v1/debug/user-signals` shows changes immediately.
* **Production:** `ServiceBusUserEventSink` (already coded) publishes to queue; `PersonalizedFeed.Worker` consumes.

### Simple linear model vs complex ML

* **Decision:** use a linear model with weights provided in JSON per tenant.
* **Why:** highly explainable in an interview; easy to tune and reason about; no ML infra required for prototype.
* **Future:** weights can be learned from data and pushed via CMS without code changes.

### No HTTP caching for feeds

* **Decision:** `Cache-Control: no-store` for `/v1/feed`.
* **Why:** feeds are highly dynamic and user-specific; caching introduces invalidation complexity that doesn’t help this prototype.
* **Future:** consider caching for non-personalized variants or aggressive per-session caching.

### User Signals Capped Affinity Categories

* **Decision:** must cap the number of categories stored in `UserSignals.CategoryStats`
* **Why:** prevents unbounded growth of user signals for users who explore many categories; keeps memory and storage usage predictable
* **Future:** implement LRU or frequency-based eviction to retain the most relevant categories

---

## 6. Rollout, logging & observability

### 6.1 Flags & rollout

* **Global kill switch**

  * `SystemConfig["Personalization.Enabled"]`.
  * When `false`, all tenants fall back to non-personalized mode.
* **Per-tenant flag**

  * `TenantConfig.UsePersonalization`.
  * Enables staged rollout by tenant.
* **Dark launch**

  * Compute personalized and baseline feeds in parallel, log comparisons, but still return baseline until metrics show improvement.

---

### 6.2 Logging & telemetry (Aspire + OTEL)

Logging/telemetry are based on `.NET Aspire`’s `ServiceDefaults`:

* `AddServiceDefaults` configures:

  * OpenTelemetry logging.
  * Metrics for ASP.NET Core, HTTP clients, runtime.
  * Tracing for ASP.NET Core + outgoing HTTP.
  * Health endpoints: `/health`, `/alive` in dev.
  * Standard HTTP resilience defaults.

Local:

* Logs and traces remain in-process by default.

Production:

* Setting `OTEL_EXPORTER_OTLP_ENDPOINT` sends traces/metrics to OTLP-compatible backends (e.g., Azure Monitor / Application Insights).

Domain services and repositories can use `ILogger<T>` for targeted logs such as:

* Missing or unknown video IDs during ingestion.
* Bad payloads / deserialization errors.
* Large, unusual event batches.

---

### 6.3 Metrics & dashboards (first iteration)

**Adoption**

* Metric:

  * Share of `/v1/feed` responses with `mode = "personalized"` vs `mode = "fallback"`, by tenant.
* Dashboard:

  * Stacked chart of personalised vs fallback traffic over time.

**Latency**

* Metrics:

  * p50/p95/p99 latency for `/v1/feed`.
* Dimensions:

  * endpoint, `mode`, `tenantId`.
* Targets (design SLOs):

  * p95 < 250 ms, p99 < 600 ms for 20 items.

**Cold-start behaviour**

* Metrics:

  * Fraction of requests where `UserSignals` is not found.
  * Once feedback is available: CTR / watch time for cold vs warm users.
* Use:

  * Evaluate effectiveness of fallback behaviour.

**Events pipeline**

* Metrics:

  * Queue length of `user-events`.
  * Worker processing lag (`now - oldest unprocessed event`, or `now - UserSignals.UpdatedAt` for recent users).
* SLO:

  * Overall signal incorporation delay ≤ 5 minutes.

**Reliability & errors**

* Metrics:

  * Error rate per endpoint.
  * Worker failures / retries.
  * Dead-letter queue size (if configured).

These can all be emitted via OpenTelemetry and visualized in Azure or any OTLP-compatible stack.

---

## 7. Performance considerations

**Throughput and latency (design targets)**

Targets:

* Peak ~3k RPS on `/v1/feed`.
* p95 < 250 ms, p99 < 600 ms for 20 items.

Read path is designed to support this by:

* Keeping the API stateless and horizontally scalable behind Front Door / API Management.
* Limiting each request to a small bounded number of data store calls:

  * load `TenantConfig`,
  * load `UserSignals` (if any),
  * fetch candidate videos.
* Running ranking entirely in memory over a small candidate set (e.g. 200 videos).

Prototype runs entirely in-memory (so perf is effectively “unbounded” for these small numbers), but in a real Azure deployment, these SLOs would be validated by:

* Deploying multiple API instances.
* Running a load test (k6 / Azure Load Testing) with realistic traffic.
* Observing p95/p99 latency via OpenTelemetry metrics.

**Freshness targets**

* **New content visible ≤ 60s**
  CMS writes new videos into `Videos`.
  As soon as they’re active and any caches have refreshed (Redis TTL tuned accordingly), they are eligible in candidate selection.

* **User signals incorporated ≤ 5 min**
  Path: `/v1/events/batch` → Service Bus → Worker → `UserSignals` + Redis.
  SLO: `queue lag + worker time + cache update` ≤ 5 minutes.
  Dashboards track queue age and the delta between `now` and `UserSignals.UpdatedAt` for recently active users.

---

## 8. Maturity policy (content safety enforcement)

Maturity policy ensures that a child or restricted-profile user **never** receives adult-rated videos, regardless of personalization.

### 8.1 Policy overview

* Each tenant defines a **maturity policy** (e.g. `"G"`, `"PG"`, `"PG13"`, `"R"`, `"NC17"`).
* Each video carries a `MaturityRating` with the same scale.

Guarantees:

* Only videos whose rating is **less than or equal** to the tenant’s policy are eligible.
* Unknown/malformed ratings are treated as **most restrictive** (fail-closed).
* Personalization cannot “resurrect” disallowed content; it only reorders already-filtered candidates.

---

### 8.2 Domain-level enforcement & repository behavior

The enforcement logic is centralized in:

```text
PersonalizedFeed.Domain/Policies/MaturityRatingPolicy.cs
```

Providing:

```csharp
bool MaturityRatingPolicy.IsAllowed(string videoRating, string policyRating);
```

`IVideoRepository.GetCandidateVideosAsync` uses this policy:

```csharp
MaturityRatingPolicy.IsAllowed(video.MaturityRating, tenant.MaturityPolicy)
```

This ensures:

* Disallowed videos are filtered before ranking.
* Any change of ranking model cannot bypass maturity rules.
* The same policy is applied consistently in all repository implementations (in-memory, SQL, etc.).

---

### 8.3 Tests & safety principles

Tests validate:

* Rating ordering (G < PG < PG13 < R < NC17).
* Handling of unknown/malformed ratings.
* Strict policies rejecting adult content.
* Liberal policies admitting all allowed content.

Safety principles:

* **Fail-closed:** Unknown ratings are treated as adult → filtered out.
* **Hard guardrail:** Filtering occurs before personalization.
* **Tenant-controlled:** CMS sets maturity policy per tenant.
* **Model-agnostic:** Ranking models cannot override filtering.
* **Low overhead:** Filtering is a simple O(N) pass over candidates.

---

## 9. With more time

Given more time, the next steps would be:

1. **Swap in real infra**

   * Add `Infrastructure.Sql` project backed by Azure SQL (or SQLite locally).
   * Add Redis-backed implementations of `ITenantConfigRepository`, `IUserSignalsRepository`.
   * Wire the app to real Azure resources via Aspire AppHost (SQL, Redis, Service Bus, Blob, Application Insights).

2. **Improve ranking models**

   * Train simple ML models (logistic regression / GBMs) on 90 days of events.
   * Experiment with vector embeddings for YouTube videos, by both metadata and content (transcript + maybe frames) to do cosine similarity search.
   * Export learned weights into `RankingModelPayloadJson`.
   * Use bandits or A/B testing to compare against the current linear model.

3. **Richer features**

   * Time-of-day, device, and short-term session-level features.
   * Normalized category-level watch time and skip rates.

4. **Better diversity / quality**

   * Generate embeddings per video from offline ML pipelines.
   * Apply MMR or similar algorithms on top of linear scores for more diversified feeds.

5. **Operational hardening**

   * Rate limiting for `/v1/feed` and `/v1/events/batch`.
   * Alerting on latency SLO violations, queue lag, error rates.
   * Stronger API key management (rotation, scoping, maybe tenant-specific rate limits).

6. **Horizontal Scaling**

   * Scale both API and Worker if load demands it

7. **Improved Observability**

  * More detailed logging
  * Dashboards and alerts around p95 and p99 latencies

8. **Different Feed Pagination**

  * Token-based or snapshot approach to either keep track of what videos this user has recently seen or keep track of last fetched video score

The current prototype intentionally keeps the **core domain model, ranking pipeline, and configuration story stable**, so these extensions are incremental rather than requiring a redesign.
