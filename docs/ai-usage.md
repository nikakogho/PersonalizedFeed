# AI Usage – PersonalizedFeed take-home

## What AI tools I used
I used ChatGPT (GPT-5.1 Thinking) as a **design and coding assistant**, mainly for:
- brainstorming trade-offs in possible high-level architectures and finalizing on one (API + Worker + SQL/Redis/Service Bus)
- sketching initial interfaces (e.g., `IRanker`, `IFeatureExtractor`, `IUserEventIngestionService`)
- generating some boilerplate (controller shapes, DI setup, Service Bus worker stub)
- getting quick feedback on trade-offs (inline vs async ingestion, in-memory vs SQL for a prototype)

Everything went through critique and code review and I wrote tests to pin behaviour down.

## What worked well
- **Architecture sparring partner**  
  It was useful for quickly discussing different designs and iterating on shapes:
  - separating Domain / Infrastructure / Api / Worker
  - designing the ranking pipeline as features + model + diversifier
  - thinking through event ingestion and future Azure resources
- **Boilerplate & glue**  
  AI was helpful for:
  - ASP.NET Core controller scaffolding
  - DI registration snippets
  - background worker skeleton with Service Bus
- **Test boilerplate**  
  It does a decent job of generating general common sense tests of a passing case, an edge case and a failing case.

## What didn’t work / where I had to be careful
- **DI lifetimes & subtle bugs**  
  At one point, the suggested worker was injecting a scoped service (`IUserEventIngestionService`) into a singleton (`UserEventsWorker`). That’s technically wrong; I fixed it by using `IServiceScopeFactory` and creating scopes per message.  
  This is a good example of where AI can produce code that seems fine but needs careful review.
- **Overly chatty / over-engineered suggestions**  
  Sometimes the AI would propose overengineering or underengineering for the task at hand. It needs to be given and reminded boundaries.
- **Port / config assumptions**  
  It occasionally assumed default ports or config approaches that didn’t match my actual setup. I treated it as suggestions, not as ground truth.
- **Forgetting context**  
  Sometimes it would lose track of previous context or details, especially over longer conversations. I had to re-provide context or correct it.
  It begins to form inconsistencies once context enlarges too much by long conversation. Sometimes requires creating a summarizing prompt and starting in a new thread with it.
- **IReadOnlyDictionary in Shouldly**
  It suggested using `ShouldContainKey` on `IReadOnlyDictionary`, but that method is only for **IDictionary**. I had to adjust the test to use `.Keys.ShouldContain` instead.

## How I would think about AI assistance for a team
If I joined, I’d treat AI as:
- a **pair programmer** for boilerplate and exploration:
  - generating initial versions of controllers, DTOs, tests, and documentation
  - trying alternative designs quickly
- a **review assistant**, not a replacement for human reviews:
  - we still do code reviews
  - we still run tests and checks
  - any AI-generated code is subject to the same standards as human-written code
- a **knowledge accelerator**:
  - using AI for quick "how do we do X with Azure Service Bus / Aspire / EF Core" questions (but double and triple-checking for hallucinations)
  - verifying everything, most of all anything that touches security, privacy, or performance

Culturally, I’d want:
- explicit guidelines on where AI is allowed (e.g., not for secrets, not for copying closed IP)
- clear ownership: the engineer who commits the code owns it, regardless of how much AI helped
- focus on **using AI to free time for the hard parts** (architecture, trade-offs, debugging), not to avoid thinking
- since this is an evolving field, all members must be allowed to experiment and share learnings

Example useful mini-prompt:
"Be maximally truth seeking and anti-sycophantic" - reasonably effective at stopping it from blindly agreeing with everything
