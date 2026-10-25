# SEEK developer-role research — 18 October 2026

Research only. No implementation, deployment or change to the agreed project plan. This report and the [audit inventory](seek-developer-sample-2026-10-18.json) support a later planning decision.

## Finding and recommendation

**Reposition OnboardFlow toward junior software/full-stack development, while retaining the onboarding business scenario as a candidate.** The evidence does not justify abandoning it merely because the previous automation search contained senior roles. It does justify changing which capabilities lead the portfolio story.

The clearest repeated requirements in this sample are communication, testing, APIs, databases, debugging and delivery practices. A working business application can demonstrate all of these. A Make workflow alone gives much less evidence of application engineering.

This is a recommendation, not an agreed scope change. Frontend, backend and UI remain undecided. There is no proposal to resume the archived build.

## Sample and limits

I screened **100 ad appearances per search group: 300 appearances, 264 distinct SEEK job IDs**, reading each distinct ad's full live description. The groups overlap. **These are not 300 verified junior vacancies.**

| Search group | Ads screened | Explicit numerical 1–3-year threshold (E) | Explicit early-career, years unquantified (J) | Wider range overlaps 1–3 (B) | Other / unconfirmed (X) |
| --- | ---: | ---: | ---: | ---: | ---: |
| Brisbane | 100 | 16 | 9 | 5 | 70 |
| Australia | 100 | 17 | 19 | 4 | 60 |
| Remote | 100 | 5 | 0 | 2 | 93 |
| Distinct ads across all groups | 264 | 33 | 22 | 8 | 201 |

E includes 1+, 2+ and 3+ minimum requirements, and explicit alternatives accepting strong graduate/projects experience. It means the stated tenure threshold is compatible with **some point** within 1–3 years. A three-year role does not thereby accept a one-year applicant. Specialist skills, degree requirements, work rights and clearance conditions still apply. A senior title is not ignored; numerical fit and seniority are separate observations.

J includes graduate/junior/associate roles and mixed-level campaigns explicitly including juniors. These 22 ads provide useful additional evidence, but do not establish a numerical requirement. B's eight wider ranges are separated conservatively rather than presented as exact matches. X combines several reasons; **201 X ads does not mean 201 ads demanded more than three years**.

### Search method

- Brisbane: [junior developer, Last 30 days](https://au.seek.com/junior-developer-jobs/in-All-Brisbane-QLD?daterange=31), pages 1–4, yielded 97 distinct encountered ads; [software developer](https://au.seek.com/software-developer-jobs/in-All-Brisbane-QLD?daterange=31) results supplied the remainder.
- Australia-wide: [junior software developer, Last 30 days](https://au.seek.com/junior-software-developer-jobs/in-All-Australia?daterange=31), pages 1–4, first 100 distinct encountered ads.
- Remote: [software developer with Remote selected](https://au.seek.com/software-developer-jobs/in-All-Australia/remote?daterange=31), pages 1–3, supplied 71 distinct ads. A junior-software-developer remote search added no new IDs. [Software engineer with Remote selected](https://au.seek.com/software-engineer-jobs/in-All-Australia/remote?daterange=31), pages 1–3, expanded the group to 100.
- The live UI's **Last 30 days** selection produced `daterange=31`; Remote was the work-arrangement filter, represented by `/remote`. E ads displayed ages from 1 to 29 days at review.
- Search order was the site's displayed order. This is a convenience sample biased toward junior keywords, not a random sample, ranking of every developer vacancy, or estimate of the proportion of junior jobs in the market.
- SEEK expands results beyond the keywords and sometimes location. The Brisbane search, for example, surfaced a Perth PEP Transport role. Core E locations were checked individually; all Brisbane E ads listed Brisbane. Unrelated clinical coding, sales, administration, design and support results were not counted as developer skill evidence.
- Cached search-engine pages and withdrawn ads from an initial discovery pass were excluded from the live sample. Full third-party ad text is not reproduced.
- A development-sounding title is not sufficient: [Virgin Australia's Software Engineer ad](https://au.seek.com/job/95220494) explicitly describes primarily BAU/Level 3 production support, so it is X and contributes no developer skill tags. SRE roles with explicit software-building duties remain included and labelled.
- A job ID is the unit of analysis, not a proven independent vacancy. Sensei's Brisbane and remote ads have near-identical content but different IDs. The three cohorts must not be added together as independent markets.

**I did not establish 100 matching 1–3-year vacancies in each group.** Reaching 100 search results is not the same thing. Nor does this study establish that more matching vacancies do not exist outside the screened results.

## Skills most frequently mentioned

The main denominator is the **33 distinct E ads**. Columns use the E subset of each search group. One ad can mention several skills. Counts include duties, desirable exposure and alternative technologies; they are not counts of mandatory requirements. Synonym tagging was checked against the rendered descriptions, but attached position descriptions were not separately read.

| Skill family | Brisbane /16 | Australia-wide /17 | Remote /5 | Distinct /33 |
| --- | ---: | ---: | ---: | ---: |
| Communication / collaboration | 15 | 17 | 4 | 32 |
| Testing | 14 | 9 | 4 | 23 |
| Databases / data stores | 11 | 12 | 3 | 22 |
| APIs / REST / webhooks | 9 | 13 | 2 | 21 |
| Debugging / troubleshooting | 9 | 10 | 2 | 19 |
| Git / version control | 7 | 7 | 3 | 15 |
| SQL / relational databases | 8 | 7 | 2 | 15 |
| AWS / Azure / GCP | 7 | 9 | 1 | 14 |
| JavaScript / TypeScript | 7 | 7 | 4 | 15 |
| AI tools, development or delivery | 5 | 8 | 4 | 14 |
| CI/CD | 7 | 4 | 2 | 12 |
| Python | 5 | 7 | 2 | 12 |
| C# / .NET | 5 | 5 | 2 | 10 |
| Java | 5 | 3 | 2 | 9 |
| Azure | 5 | 6 | 1 | 9 |
| HTML / CSS | 4 | 5 | 2 | 9 |
| AWS | 3 | 5 | 1 | 8 |
| Agile / Scrum | 7 | 3 | 0 | 10 |
| React / Next.js | 3 | 5 | 1 | 8 |
| Docker | 3 | 2 | 1 | 5 |
| Node.js | 2 | 4 | 0 | 5 |
| Power Platform | 3 | 1 | 1 | 4 |
| Angular | 2 | 3 | 0 | 4 |
| C / C++ | 3 | 1 | 1 | 4 |
| GCP / Google Cloud | 0 | 2 | 0 | 2 |
| Vue | 1 | 2 | 0 | 3 |
| PHP | 1 | 1 | 1 | 2 |

Communication includes collaboration and stakeholder work. Testing includes general testing and test responsibilities; it does not mean all 23 ask for Playwright or automated QA. Databases is broader than SQL. React includes Next.js; Node.js is separate. Named cloud is the union of AWS, Azure and GCP, not the sum of those rows. AI includes coding assistants, product capabilities, delivery and curiosity/exposure; generic company-only AI wording was excluded where identified. These categories are not interchangeable or additive.

### Interpretation

1. **Build and explain dependable software.** Testing appears in 23/33 E ads, APIs in 21/33, databases in 22/33 and debugging in 19/33. Employers repeatedly describe maintaining existing systems, investigating defects and taking changes through delivery.
2. **Database and integration competence travels across stacks.** SQL/relational engines appear in 15/33, Git in 15/33 and CI/CD in 12/33. Show the data model, validation, error handling, a meaningful test and a repeatable release.
3. **There is no single winning stack in this sample.** JavaScript/TypeScript appears in 15/33, Python in 12/33, C#/.NET in 10/33 and Java in 9/33. The sample mixes web/business applications with embedded, defence and SRE roles; one application will not credibly target all of them.
4. **The wider junior group strengthens the web/.NET signal.** Combining E and J gives 55 distinct early-career-signalling ads: JavaScript/TypeScript 27, C#/.NET 21, Python 18, React/Next.js 16 and Java 11. These are sensitivity checks using a broader definition, not another set of confirmed 1–3-year requirements.
5. **AI familiarity is useful; adding a chatbot is not the implied requirement.** Many relevant ads describe using AI to develop, investigate and test software while understanding and validating the result. A documented example of catching and correcting an AI-generated mistake may demonstrate this better than an unrelated AI feature.

Brisbane's E subset mixes Java, .NET, web/business applications and defence/embedded engineering. National examples add Node/TypeScript, PHP and broader product roles. The five remote E ads are too few and too specialised to rank remote-market stacks confidently.

## What genuine opportunities look like

- [Robert Half, Brisbane](https://au.seek.com/job/95158567): 1–2 years commercial experience **or** a degree with strong personal projects; C#/.NET, SQL Server, Azure, Python and Angular are listed as ideal familiarity. AI code review and customer communication matter.
- [Austcorp, Melbourne](https://au.seek.com/job/94756812): 1–3 years **or** strong graduate/project experience; integration, web technologies, APIs and SQL. This directly supports building an explainable application with integrations.
- [Tabcorp, Sydney](https://au.seek.com/job/94851367): 1.5–2 years **commercial** backend Node.js/TypeScript experience. A project is relevant evidence, but the ad does not state that it substitutes for commercial tenure.
- [Sensei, Brisbane](https://au.seek.com/job/94959991): 2+ years consulting or solution delivery, hands-on Power Platform development and integrations, plus the ability to explain delivered projects. This is a specialist Microsoft path, not a generic junior role.
- [Semaphore, Melbourne](https://au.seek.com/job/94791902): explicitly early-career, accepts evidence from employment, internships, university or substantial personal work; C#, PHP, SQL/MySQL, HTML/CSS and debugging. Numerical years are unstated, so this is J.
- [ISQ, Brisbane](https://au.seek.com/job/94915354): a graduate title still asks for two years of Dynamics/Power Platform and one year of cloud exposure. Titles alone are unreliable eligibility filters.

Some low numerical thresholds accompany significant gates. [Boeing](https://au.seek.com/job/95085104) needs Australian citizenship and NV1 eligibility; [KBR](https://au.seek.com/job/94847910) requires active NV1 or above. [Shura's remote Salesforce role](https://au.seek.com/job/94922949) also requires citizenship and NV1, and [Axon's remote graduate SRE role](https://au.seek.com/job/94800182) requires citizenship/clearance eligibility. [Askable](https://au.seek.com/job/95299570) is contract AI-coding evaluation work, not an ordinary junior product-engineering vacancy.

Remote means the advertised arrangement, not permission to work from any country. [Jet Convert](https://au.seek.com/job/95156813) specifies remote within Australia. The remote sample also surfaced graduate support/testing roles, but they are adjacent pathways and excluded from developer skill counts: [example](https://au.seek.com/job/94885562).

## Implications for OnboardFlow

### Recommended positioning

**A small client-onboarding web application that demonstrates junior full-stack engineering.** The visitor understands the business problem and can complete a useful workflow. The code and case study then explain how the frontend, API, database and tests make it reliable.

The previous automation sample was too senior-heavy and too broad to select the project's primary hiring audience confidently. This research supports targeting junior developer, software engineer and integration-developer opportunities first. QA and automation remain supporting capabilities.

### Retain

- One clear fictional agency/client scenario and a short employer walkthrough.
- A useful create → review → correct → approve → handoff flow.
- SQL-backed records, explicit state transitions and validation.
- Tests of business rules, invalid requests, repeated actions and one recoverable failure.
- A resettable demo without visitor registration or personal data.
- A readable case study covering the problem, design choices, limitations and actual verification.

### Proposed simplification, subject to agreement

- Make the working application the main deliverable and portfolio claim.
- Treat Make/Google Sheets as a later supporting integration rather than the core completion gate. Use a labelled sandbox handoff in the employer demo.
- Keep one visible repeat/failure example. Avoid turning the first release into a general workflow engine or a large retry/observability platform.
- Keep AI optional. Demonstrate reviewed AI-assisted engineering without requiring a paid model or making a model API necessary for the demo.
- Measure success by what a visitor can complete and what Dexter can explain, rather than the number of technologies integrated.

### Technology decisions to make next

Two coherent directions deserve comparison when frontend/backend planning begins:

| Candidate direction | Employer signal | Main tradeoff |
| --- | --- | --- |
| TypeScript + React + a Node.js API + SQL | Web/product development, integration work, shared language across the application | Must show real backend/data work; choose hosting only after checking ongoing free eligibility. |
| TypeScript/React UI + C#/ASP.NET Core API + SQL | Strong .NET signal in the wider early-career sample and Microsoft-oriented business applications | A second language and potentially different zero-cost deployment constraints. |

Python is also represented, but some of its signal comes from scientific, AI, SRE and embedded-adjacent work. Do not introduce a third backend just to collect keywords. Java is a credible separate employment path; it is not evidence that this project needs two backends.

No frontend/backend/framework choice is approved here. A Node or .NET API should not be claimed as a skill demonstrated by a different runtime merely because both use similar architectural ideas.

### Zero-cost boundary

Continue the existing zero-additional-cost rule. Prefer an application that can be developed and demonstrated with local/open-source tooling; assess the public hosting arrangement separately before committing to it. A free plan's eligibility, quotas, sleep behaviour and ability to serve the employer demo need live verification at the architecture stage.

No paid AI, email or CRM calls are needed to prove the main developer skills. Keep fictional messages in the Outbox. Optional integrations must not become necessary for a visitor's successful walkthrough. This study did not re-audit provider pricing or approve a hosting plan.

### Decision rule

Keep OnboardFlow if the next high-level review produces a convincing business flow and a manageable application demonstrating the skills above. Change the scenario if it still feels artificial or takes too much explanation. Changing the business domain alone will not solve an experience requirement.

A portfolio can demonstrate engineering ability and provide interview evidence. It must remain labelled as a personal project; it does not create commercial employment tenure.

## Appendix: all numerical experience matches

E is numerical compatibility, not a claim that these are all junior jobs or that Dexter meets every criterion. The inventory retains skill tags, location and search-group membership.

| Ad | Search group(s) | Posted age at review | Experience and material caveat |
| --- | --- | ---: | --- |
| [NWI Group: Software Engineer](https://au.seek.com/job/94726985) | Australia | 29d | Two or more years, focused on full-stack SaaS. |
| [Austcorp Executive: Junior Software Developer - Integration](https://au.seek.com/job/94756812) | Australia | 26d | One to three years commercial experience OR strong graduate/project experience. |
| [Geoimage Pty Ltd: Software Engineer — Satellite Imagery & Geospatial Systems](https://au.seek.com/job/94768050) | Brisbane, Australia | 26d | Two or more years and production delivery evidence. |
| [MiClub Services: Software Developer (Java)](https://au.seek.com/job/94795412) | Australia | 25d | Three or more years commercial Java, completed degree, web security skills. |
| [Auto & General: Java Developers](https://au.seek.com/job/94797099) | Brisbane | 25d | Two or more years development or relevant industry experience. |
| [Axon: Graduate Site Reliability Engineer (Australia)](https://au.seek.com/job/94800182) | Remote | 25d | About one year software/SRE/infrastructure; Australian citizenship and clearance eligibility. |
| [Auto & General: Full Stack Developer](https://au.seek.com/job/94844653) | Brisbane | 23d | Three or more years; explicitly welcomes less experienced applicants for junior opportunities. |
| [KBR: Software Engineer](https://au.seek.com/job/94847910) | Brisbane | 23d | Minimum three years; active NV1 or above required. |
| [Tabcorp: Junior Software Engineer](https://au.seek.com/job/94851367) | Australia | 23d | 1.5-2 years commercial backend Node.js and TypeScript. |
| [Quikky Move: Software Engineer – Web, Mobile & FinTech Platform](https://au.seek.com/job/94866711) | Australia | 23d | Three or more years web/mobile software development. |
| [Cloud pro Technologies: Software engineer](https://au.seek.com/job/94879548) | Brisbane | 22d | Minimum one year in software development. |
| [Insitu Pacific: Engineer - Software](https://au.seek.com/job/94890841) | Brisbane | 22d | Two or more years electronic/software product engineering; Australian citizenship and clearance eligibility. |
| [Independent Schools Queensland: Graduate Software Engineer 2 year fixed term contract](https://au.seek.com/job/94915354) | Brisbane, Australia | 19d | Two years Dynamics 365/Power Apps/Power Automate plus one year cloud. |
| [Shura: Senior Salesforce Developer](https://au.seek.com/job/94922949) | Remote | 19d | Three or more years Salesforce; Australian citizenship and minimum NV1. |
| [Dovida: Software Developer](https://au.seek.com/job/94932262) | Brisbane, Australia | 18d | At least 2-3 years software development. |
| [Sensei: Technical Consultant](https://au.seek.com/job/94959991) | Brisbane | 17d | Two or more years consulting or solution delivery; hands-on Power Platform code and integrations. |
| [Sensei: Technical Consultant](https://au.seek.com/job/94960073) | Remote | 17d | Two or more years consulting or solution delivery; near-identical campaign to 94959991. |
| [M&T Resources: GEN Developer](https://au.seek.com/job/94988916) | Brisbane | 16d | Minimum two years Gen development; leadership duties and Baseline eligibility. |
| [Google: Software Engineer, Backend, Photos](https://au.seek.com/job/95007512) | Australia | 16d | Two years backend development plus Android/iOS client experience. |
| [NOJA Power: Embedded Software Developer](https://au.seek.com/job/95076712) | Brisbane | 11d | Three or more years embedded C/C++ Linux; specialised hardware/software skills. |
| [Boeing: MQ-28 Software Engineer](https://au.seek.com/job/95085104) | Brisbane | 11d | Minimum two years in industry; autonomous systems/robotics; citizenship and NV1 eligibility. |
| [Google: Software Engineer, Android SRE](https://au.seek.com/job/95094122) | Australia | 11d | Two years software development; two years distributed-systems experience is preferred. |
| [Evse Australia: Software Engineer](https://au.seek.com/job/95099926) | Australia | 10d | Two or more years engineering or related technical field. |
| [McIlwain Civil Engineering: Digital Solutions Engineer](https://au.seek.com/job/95100426) | Brisbane | 10d | Two or more years building and shipping working solutions. |
| [Austal Ships: Software Engineer](https://au.seek.com/job/95112854) | Australia | 10d | Two-year technical requirements; degree/certifications OR five years equivalent experience. |
| [Flight Centre Travel Group: Software Engineer - South Bank, QLD](https://au.seek.com/job/95140380) | Brisbane | 9d | At least three years professional software development. |
| [Jet Convert Pty Ltd: Full Stack PHP Developer / AI & Automation / Fully Remote](https://au.seek.com/job/95156813) | Remote | 8d | At least two years professional development; remote within Australia; ownership expected. |
| [Robert Half: Junior Agentic Software Developer / I.T Services](https://au.seek.com/job/95158567) | Brisbane, Australia | 8d | Degree and strong projects OR 1-2 years commercial development. |
| [Docuscan Australia Pty Ltd: Software Engineer - Full Stack AI](https://au.seek.com/job/95167622) | Australia | 8d | Ad contains both 2+ and 3+ requirements; treated as three years; Melbourne-based required. |
| [Pronto Software: Developer](https://au.seek.com/job/95219767) | Australia | 4d | At least two years in a development team. |
| [Google: Software Engineer, Payments Identity Verification](https://au.seek.com/job/95271703) | Australia | 3d | One year software development; degree or equivalent practical experience. |
| [South Pacific Seeds Pty Limited: Software Developer](https://au.seek.com/job/95278039) | Australia | 2d | At least two years development in a similar role. |
| [Askable: Senior Software Engineer, AI Coding Tasks (Contract)](https://au.seek.com/job/95299570) | Brisbane, Remote | 1d | Two or more years; AI coding evaluation contract, minimum ten hours weekly. |

## Appendix: explicit early-career ads without numerical tenure

These J ads are useful application/research leads, subject to their full requirements and graduate-program eligibility. Mixed-level ads can list technologies across several grades.

- [DingGo: Junior Software Developer](https://au.seek.com/job/94721572)
- [Semaphore Consulting: Early-Career Software Developer](https://au.seek.com/job/94791902)
- [Private Advertiser: Graduate AI & Technology Developer](https://au.seek.com/job/94796145)
- [The Recruitment Alternative: Software Developer - Graduate](https://au.seek.com/job/94816727)
- [ASTA Solutions: Junior Software Developer](https://au.seek.com/job/94817777)
- [Emanate Technology: Developer/Support](https://au.seek.com/job/94908023) — combines explicit .NET/API/SQL development with service-desk and systems duties; assess the actual work split.
- [SEEK: Graduate Software Engineer](https://au.seek.com/job/94933963)
- [Clockon: Junior Software Developer - .Net / C#](https://au.seek.com/job/94960280)
- [Quality People: Junior Software Developer](https://au.seek.com/job/95003612)
- [University of Melbourne: AURIN Associate Software Engineer](https://au.seek.com/job/95049289)
- [Real Time Monitoring: Junior Programmer – Entry Level](https://au.seek.com/job/95110025)
- [Green Innovation Systems: Junior Software Engineer](https://au.seek.com/job/95110759)
- [TechnologyOne: Graduate Developer / Developer - DXP Platform](https://au.seek.com/job/95134614)
- [MinDet: Software Developer](https://au.seek.com/job/95152713)
- [Tyree Group: Junior Software Engineer](https://au.seek.com/job/95153832)
- [Stanford Innovations: Software Engineer / Full Stack Engineer (All Levels, Several Positions)](https://au.seek.com/job/95158006) — mixed-level ad; includes junior opportunities.
- [FTI Group: Junior C# Developer – AWS, DevOps & AI Automation](https://au.seek.com/job/95198058)
- [Intuit Technologies: Graduate Software and Power Platform Developer](https://au.seek.com/job/95231762)
- [Auto & General: Front End Developer](https://au.seek.com/job/95234347) — mixed-level ad; includes junior opportunities.
- [SEEK Grad: 2027 DXC Technology Graduate - AI Engineer - Melbourne](https://au.seek.com/job/95260509)
- [Quartex: Associate Software Engineer](https://au.seek.com/job/95262058)
- [Private Advertiser: Senior & Junior Application Developers – SharePoint Online & Power Platform](https://au.seek.com/job/95302173) — mixed-level ad; includes junior opportunities.

## Appendix: overlapping wider ranges

| Ad | Stated range / caveat |
| --- | --- |
| [Canstar: Full Stack Developer](https://au.seek.com/job/94914931) | 3-5 years |
| [Sidekicker: Software Engineer](https://au.seek.com/job/94931695) | Preferred 3-6 years |
| [Pitch Black Group: Software Developer](https://au.seek.com/job/94974957) | 3-8 years |
| [BizCover: Junior Data Engineer](https://au.seek.com/job/95069872) | Ideally 3-4 years; adjacent data-engineering role |
| [LAPM: Junior-Mid Full-Stack Developer](https://au.seek.com/job/95171312) | Approximately 2-4 years; commercial OR strong practical-project portfolio |
| [Inhabit Group: Rail Software and Systems Engineer](https://au.seek.com/job/95197406) | Ideally 3-5 years |
| [APNIC: Software Engineer](https://au.seek.com/job/95231667) | 2-5 years |
| [PEP Transport: Developer](https://au.seek.com/job/95282018) | 3-5+ years; Perth role surfaced in Brisbane search |

The [machine-readable audit](seek-developer-sample-2026-10-18.json) lists every screened job ID, all three 100-ad search inventories, classification definitions and the counts used above. X entries deliberately retain the combined unconfirmed/excluded category; no unsupported individual tenure claim is made for them.

