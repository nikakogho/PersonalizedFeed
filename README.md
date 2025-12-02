# PersonalizedFeed – .NET 8 backend for personalized vertical video feeds

This repo contains a **.NET 8** backend prototype for **personalised vertical video feeds** (TikTok-style).

The goal is to show **architecture + code quality + extensibility**, not a full production deployment.

- API framework: **ASP.NET Core (.NET 8)**
- Orchestration: **.NET Aspire** (AppHost project) for local multi-service runs
- Projects:
  - `PersonalizedFeed.Domain` – domain model, ranking & services
  - `PersonalizedFeed.Infrastructure` – in-memory persistence (prototype)
  - `PersonalizedFeed.Api` – HTTP API (`/v1/feed`, `/v1/events/batch`, debug endpoints)
  - `PersonalizedFeed.Worker` – background worker shape for async event ingestion (Azure Service Bus)
  - `PersonalizedFeed.Domain.Tests` – unit / integration tests

The repo is structured so that:

- **Local demo** runs with **no Azure dependencies** (everything in-memory, inline ingestion).
- **Production design** is clearly visible: API + Worker + Azure SQL + Redis + Service Bus + VNet.

---

## Design goals (from the assignment)

The system supports a new **Personalised Video Feeds** feature with:

- **Scale**: peak ~ 3k RPS, avg ~ 600 RPS (design target)
- **Latency**: p95 < 250 ms, p99 < 600 ms for 20 items (design target)
- **Freshness**:
  - new content visible <= 60 seconds
  - user signals incorporated <= 5 minutes
- **Privacy**:
  - SDK sends **hashed user ID** only
  - user-level data stays inside a private network / VNet in production
- **Multi-tenancy**: ~120 tenants; each can customize ranking weights
- **Feature flags**:
  - per-tenant personalization on/off
  - global kill switch back to non-personalized feed

This repo implements the **core backend shape + prototype**, with:

- a real ranking pipeline (feature extraction + linear model + diversification),
- an events ingestion path updating `UserSignals` aggregates,
- clear extension points for Azure resources and ML upgrades.

---

## Project structure

```text
PersonalizedFeed/
  README.md
  PersonalizedFeed.sln
  src/
    PersonalizedFeed.Api/
    PersonalizedFeed.AppHost/
    PersonalizedFeed.Domain/
    PersonalizedFeed.Infrastructure/
    PersonalizedFeed.ServiceDefaults/
    PersonalizedFeed.Worker/
  tests/
    PersonalizedFeed.Domain.Tests/
````

High-level responsibilities:

* **Domain**
  * Entities: `TenantConfig`, `Video`, `UserSignals`, `UserEvent`, `UserEventBatch`, etc.
  * Ranking: `RankingFeatures`, `IFeatureExtractor`, `IRankingModel`, `LinearRankingModel`, `IFeedDiversifier`, `SimpleFeedDiversifier`, `IRanker`, `Ranker`.
  * Services: `IFeedService`, `IUserEventIngestionService`.
* **Infrastructure**
  * In-memory repositories:
    * `ITenantConfigRepository`
    * `IVideoRepository`
    * `IUserSignalsRepository`
    * `IVideoStatsRepository`
  * Seed data for demo (see below).
* **Api**
  * HTTP endpoints:
    * `GET /v1/feed`
    * `POST /v1/events/batch`
    * `GET /v1/debug/user-signals`
    * (`POST /v1/debug/tenant/personalization` – debug-only toggle)
  * `IUserEventSink` abstraction:
    * `InlineUserEventSink` – default for local demo (directly calls ingestion service)
    * `ServiceBusUserEventSink` – production shape (publishes to Azure Service Bus)
* **Worker**
  * `UserEventsWorker` reading from Service Bus queue `user-events`
  * Processes `UserEventBatch` messages and calls `IUserEventIngestionService`
  * Not required for local demo, but shows production-ready async pipeline.

---

## Seed data (for local demo)

The in-memory infrastructure seeds:

* **Tenant**

  ```text
  TenantId: tenant_1
  ApiKey:   secret-api-key
  ```

* **Ranking model**

  A simple linear model with higher weight on **CategoryAffinity** and a recency penalty:

  ```csharp
  var weights = new LinearWeights(
      CategoryAffinity:       15.0,
      RecencyHours:           -0.1,
      GlobalPopularityScore:  0.5,
      EditorialBoost:         0.0,
      UserWatchTimeLast7d:    0.0,
      UserSkipRateLast7d:     0.0,
      IsMatureContent:       -100.0);
  ```

  Stored as JSON in `TenantConfig.RankingModelPayloadJson`.

* **Videos**

  ```text
  tenant_1 / vid_fitness_1
    - Title: "Fitness warmup"
    - MainTag: "fitness"
    - Duration: 30s
    - Maturity: PG
    - GlobalPopularityScore: 5

  tenant_1 / vid_cooking_1
    - Title: "Cooking pasta"
    - MainTag: "cooking"
    - Duration: 30s
    - Maturity: PG
    - GlobalPopularityScore: 20
  ```

* **User signals**

  For `user_hash_123`:

  ```text
  fitness: views=8, watchTimeMs=120_000, skips=1
  cooking: views=2, watchTimeMs=10_000, skips=0

  TotalViewsLast7d:      10
  TotalWatchTimeLast7d:  130_000ms
  SkipRateLast7d:        0.1
  ```

Effect:

* For `user_hash_123`, the ranking pipeline *should* favor fitness videos more than raw popularity would suggest.
* For a new user (e.g. `user_hash_999`), the system behaves like a popularity-based feed (fallback).

---

## How to run locally

### Prerequisites

* [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download)
* Any HTTP client:

  * curl, Postman, or Visual Studio’s HTTP file support
* (Optional) Visual Studio / Rider for debugging.

### 1. Restore & build

From repo root:

```bash
dotnet restore
dotnet build
dotnet test
```

`dotnet test` runs unit + integration tests for the domain layer and basic services.

### 2. Run the API (local demo mode)

From repo root:

```bash
cd src/PersonalizedFeed.Api
dotnet run
```

By default this runs on `http://localhost:5000` and `https://localhost:5001`.

Key points:

* Uses **in-memory** infrastructure (`AddInMemoryInfrastructure()`).
* Uses **inline** event ingestion:

  ```csharp
  services.AddScoped<IUserEventSink, InlineUserEventSink>();
  ```

  So `POST /v1/events/batch` updates `UserSignals` immediately inside the API process.

We do **not** need Azure, SQL Server, Redis, or Service Bus to run this demo.

> To run everything via .NET Aspire, use the `PersonalizedFeed.AppHost` project.
> For simplicity, it’s enough to run `PersonalizedFeed.Api` directly.

---

## HTTP API – endpoints & example calls

All examples assume:

* Base URL: `https://localhost:7207`
* Headers (for seeded tenant):

  ```text
  X-Tenant-Id: tenant_1
  X-Api-Key:   secret-api-key
  X-User:      user_hash_123
  ```

You can use any other `X-User` to simulate a new user without existing signals.

### 1. GET /v1/feed – fetch a personalized feed

**Request**

```http
GET https://localhost:7207/v1/feed?limit=20
X-Tenant-Id: tenant_1
X-Api-Key: secret-api-key
X-User: user_hash_123
```

**Response (shape)**

```json
{
  "mode": "personalized",        // or "fallback" if personalization disabled
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
      "score": 12.34,            // ranking score
      "rank": 0                  // position in feed
    },
    {
      "videoId": "vid_cooking_1",
      "playbackUrl": "https://cdn.example.com/v/vid_cooking.m3u8",
      "thumbnailUrl": null,
      "title": "Cooking pasta",
      "mainTag": "cooking",
      "tags": ["cooking"],
      "durationSeconds": 30,
      "maturityRating": "PG",
      "score": 8.90,
      "rank": 1
    }
  ],
  "nextCursor": "offset:20"
}
```

Behavior:

* For `user_hash_123`, fitness may be ranked above cooking due to `CategoryAffinity`.
* For a new user (e.g. `user_hash_999`), `mode` will still be `personalized` but `UserSignals` will be null and fallback to popularity-based scoring.

---

### 2. GET /v1/debug/user-signals – inspect internal signals (debug only)

This is a **diagnostic endpoint** to show what the system knows about a user.
Not intended for production.

**Request**

```http
GET https://localhost:7207/v1/debug/user-signals?userHash=user_hash_123
X-Tenant-Id: tenant_1
X-Api-Key: secret-api-key
```

**Response (for seeded user)**

```json
{
  "tenantId": "tenant_1",
  "userHash": "user_hash_123",
  "categoryStats": {
    "fitness": {
      "views": 8,
      "watchTimeMs": 120000,
      "skips": 1
    },
    "cooking": {
      "views": 2,
      "watchTimeMs": 10000,
      "skips": 0
    }
  },
  "totalViewsLast7d": 10,
  "totalWatchTimeLast7dMs": 130000,
  "skipRateLast7d": 0.1,
  "lastActiveAt": "2025-01-01T10:00:00+00:00",
  "updatedAt": "2025-01-01T11:00:00+00:00"
}
```

For a new user (`user_hash_999`), you’ll get `404 NotFound` until you send events.
