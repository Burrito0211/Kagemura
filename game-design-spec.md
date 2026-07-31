# Game Design & Technical Spec

**Working Title:** Hazakura ("blade-blossom")
*Alternate name options: Kagemura ("shadow village"), Onikage ("demon shadow"), Kanshi no Katana ("the watching blade")*
**Engine:** Unity 6 (LTS), C#
**Genre:** 2D side-scrolling action, with stylized 3D background elements
**Setting:** Edo-era Japan, ukiyo-e inspired
**Target scope:** Complete, polished short-form game (~20–30 min playtime)
**Reference inspiration:** Nine Sols, Dead Cells (tone/feel only — scope is intentionally much smaller)

---

## 1. Game Overview

A linear, side-scrolling action game with tight melee-focused combat, a short handcrafted story arc, and atmospheric 3D background elements layered behind 2D gameplay. The player fights through a small sequence of levels using three weapons with distinct playstyles, culminating in a final boss fight that resolves the story.

**Design pillars (in priority order):**
1. Combat must feel good before anything else is built on top of it.
2. The game must be finishable start-to-end — no dangling systems.
3. Visual "wow" comes from 3D background/atmosphere, not gameplay complexity.

### 1.1 Core Hook / Identity

**A masked ronin cutting through yokai-corrupted land across the four seasons.**

This single premise ties together visuals, mechanics, and structure without adding new systems to build:

- **Protagonist:** A disgraced or wandering ronin, wearing a distinctive mask (strong single-image identity, doubles as a portfolio/marketing asset). Personal motivation (revenge, redemption, or seeking a lost person) drives the short story arc.
- **Enemies as yokai:** Enemies are folklore spirits (yokai) corrupting people/land, which justifies stylized, non-realistic silhouettes (easier to design than realistic human enemies) and gives combat a supernatural flavor at no extra system cost — e.g., sickle bleed reads as a curse-mark, offensive burst special reads as a brief possession state.
- **Seasonal structure:** Each level represents a season (spring → summer → autumn → winter). Palette, enemy reskins, and environmental motifs shift per season, giving visual variety by re-skinning existing planned content rather than building new content types. The final (winter) level leads into the boss.

This hook should inform naming, palette choices, and enemy/boss flavor text throughout production, but does not add new engineering scope.

---

## 2. Core Gameplay Systems

### 2.1 Movement
- Run, jump, single mid-air jump (optional stretch), fall.
- 2D physics-based (Rigidbody2D) or kinematic controller — recommend kinematic (CharacterController2D-style) for precise, non-floaty platforming feel.

### 2.2 Weapons

| Weapon | Speed | Range | Damage | Identity / Feel |
|---|---|---|---|---|
| **Melee (Sword)** | Medium | Short | Medium | Balanced default. 2-hit combo. This is the first thing to prototype and tune. |
| **Sickle** | Fast | Very short | Low per hit | High attack speed; bleed-stack or small lifesteal on hit. Rewards aggressive close play. |
| **Bow** | Slow to fire | Long | Medium-high | Resource-limited (stamina or arrow count) or has charge-up. Forces positioning decisions; can't be spammed safely. |

Implementation note: build all three off a shared `WeaponBase` class/interface (attack timing, hitbox activation, damage value, on-hit effect) rather than separate systems per weapon. Differentiate via data (ScriptableObject stats) wherever possible instead of unique code paths.

### 2.3 Dodge / Parry
- **Dodge with i-frames** (Dead Cells-style) — safer to tune, more forgiving for a first solo project. Built at step 3 and the base of the combat loop.
- **Decided: dodge + parry** (was §9's first open question, settled 2026-07-30). The parry was built on top of the dodge, not instead of it, and the two are deliberately different answers to the same moment:
  - **Dodge** — longer window, moves you clear, no punish for a miss beyond the roll's tail. The safe read, always available.
  - **Parry** — shorter window, no movement, and a whiff roots the player in place. A success drops the attacker's swing outright (including a live hitbox), staggers it, and pays bonus Focus. The greedy read.
  - A parry deals **no damage** — the reward is tempo and Focus. Making it also a free hit would leave no reason to do anything else.
  - Dodge was **not** nerfed to make room. If the parry proves strictly better in playtesting, tighten the parry or lengthen its whiff lockout before touching the dodge — the dodge is what the rest of the game is tuned against.
- Parry-only combat remains rejected as an initial design: high-risk to get feeling good.

### 2.4 Special Abilities (2 total)
1. **Offensive burst** — e.g. AoE slam or heavy strike, resource/cooldown gated.
2. **Utility/mobility** — e.g. dash-strike or short-range pull, doubles as a traversal tool for level design.

Both specials share one resource pool (e.g., "Focus") to avoid building two separate economy systems.

### 2.5 Enemies
- 2–3 base enemy types, differentiated by behavior pattern (not just stats):
  - **Type A – Melee rusher:** closes distance, punishes standing still.
  - **Type B – Ranged/turret:** punishes reckless approach, rewards dodge timing.
  - **Type C (optional) – Shielded/armored:** punishes button-mashing, rewards using the heavier weapon or specials.
- **1 final boss:** 2 attack phases, phase 2 triggered at an HP threshold with a visual tell (color change, arena change, etc.)

---

## 3. Levels & Story

### 3.1 Structure
- **4 linear levels, one per season** (Spring → Summer → Autumn → Winter), ~2–3 minutes of core gameplay each (excludes narrative beats), followed by a boss arena.
- Levels are handcrafted, not procedural. Each season introduces or re-contextualizes one enemy type or environmental hazard rather than everything at once — e.g. Spring (cherry blossoms, first yokai encounters), Summer (rain/storm hazards, second enemy type), Autumn (falling leaves, environmental traps), Winter (snow, reduced visibility, leads into boss).
- Seasonal palette shifts handle most of the "visual variety" workload without requiring new art categories per level.

### 3.2 Story

> **STATUS — DEFERRED 2026-07-31. Do not pick this up unless explicitly asked.**
>
> **Deferred, not cut.** The story stays in the design; only the authoring work is postponed.
>
> What exists: the whole delivery system — `DialogueData`, `DialogueTrigger`, `DialogueUI`
> (commit `96b7590`). It works and needs no further code.
>
> What does not exist: any content. There are zero `DialogueData` assets in the project and
> zero `DialogueTrigger` instances in any scene, so every beat is currently a no-op.
>
> Why deferred: blocked on the protagonist's motivation (§9), which is undecided — beats
> can't be written before the thing they reveal exists. §3.2 also recommends placing beats
> after combat pacing is known, and the levels (Build Order step 9) aren't built yet.
>
> To resume: decide the motivation in §9, author `DialogueData` assets, drop
> `DialogueTrigger` components into the level scenes. No new scripts required.

- Short, linear narrative following a masked ronin traveling through yokai-corrupted land across the four seasons, driven by a personal motivation (revenge, redemption, or seeking someone lost) revealed gradually.
- Structure: opening hook (why the ronin is on this path) → 1–2 mid-game story beats (one per season transition, revealing more of the motivation/backstory) → ending resolved at the boss fight.
- Delivered via short dialogue/text boxes or environmental storytelling — avoid heavy cutscene production cost.
- Recommend: define exact story beats **after** core combat is prototyped, so narrative moments can be placed at natural gameplay pacing breaks (after a hard fight, before a boss, etc.)

### 3.3 3D Background Elements
- Gameplay remains strictly 2D (2D physics, 2D colliders, sprites).
- 3D used only for non-interactive atmosphere:
  - Parallax background layers using 3D geometry + a separate background camera.
  - A rotating/idle 3D silhouette or diorama visible behind gameplay (torii gates, pine trees, Mt. Fuji-esque peaks, seasonal foliage).
  - Optionally, one 3D camera-pan cutscene moment (e.g., story reveal) using Cinemachine.
- Technical approach: separate camera stack — a 3D background camera (rendering to a layer) composited behind the 2D gameplay camera. This isolates 3D complexity from gameplay code entirely.
- **Render style for 3D layers:** unlit or toon-shaded with strong rim lighting, rather than realistic lighting — this mimics the flat, poster-like look of ukiyo-e prints and is significantly less work to get looking good than realistic 3D lighting.

---

## 4. Art Direction

**Style: "Ukiyo-e stylized"** — flat, poster-like color blocking evoking Edo-period woodblock prints, rather than painterly or realistic rendering.

- **Palette:** 4–6 colors per scene max — deep indigo, vermillion red, aged parchment cream, ink black, plus one accent color that shifts per season.
- **Silhouettes:** Strong, readable silhouettes for enemies and background elements — favor bold shapes over fine detail, both for visual clarity in combat and to reduce art workload.
- **Recurring motifs:** Wave/cloud patterns (Hokusai-style), cherry blossoms, torii gates, paper lanterns — cheap to reuse across levels while instantly reading as "Edo Japan."
- **Seasonal palette guide:**
  - Spring — soft pink/cream, cherry blossom accents
  - Summer — deep green/indigo, storm-grey accents
  - Autumn — burnt orange/vermillion, falling leaves
  - Winter — cool blue/white, ink-black silhouettes for the boss arena
- **Protagonist:** Masked ronin — mask design should be a strong, simple, instantly recognizable silhouette, since it's the game's primary visual identity.
- **Enemies (yokai):** Non-realistic, folklore-inspired silhouettes rather than realistic human/creature designs — easier to design solo and reinforces the supernatural tone.

---

## 5. UI / Menus
- Main menu (Start, Quit, maybe Settings)
- Pause menu (Resume, Restart, Quit to menu)
- HUD: health, resource/Focus meter, current weapon indicator
- Game over screen
- Win/ending screen

---

## 6. Technical Architecture (Unity + Claude Code notes)

Suggested folder/project structure to keep Claude Code sessions scoped and easy to navigate:

```
Assets/
  _Project/
    Scripts/
      Player/
        PlayerController.cs
        PlayerHealth.cs
        WeaponBase.cs
        Weapons/
          SwordWeapon.cs
          SickleWeapon.cs
          BowWeapon.cs
        Abilities/
          SpecialBase.cs
          OffensiveBurst.cs
          MobilityDash.cs
        DodgeController.cs
      Enemies/
        EnemyBase.cs
        EnemyRusher.cs
        EnemyRanged.cs
        BossController.cs
        BossPhase1.cs / BossPhase2.cs (or state machine)
      Systems/
        GameManager.cs
        HealthSystem.cs
        ResourceSystem.cs (Focus meter)
        DamageSystem.cs
      UI/
        HUDController.cs
        PauseMenu.cs
        MainMenu.cs
      Narrative/
        DialogueTrigger.cs
        DialogueUI.cs
    ScriptableObjects/
      WeaponData/
      EnemyData/
      DialogueData/
    Scenes/
      MainMenu.unity
      Level_01.unity ... Level_05.unity
      BossArena.unity
    Prefabs/
    Art/
      Sprites/
      3D_Background/
    Audio/
```

**Design pattern recommendations:**
- **ScriptableObjects for weapon/enemy stats** — lets you tune damage/speed/range without touching code, and keeps Claude Code changes localized to data assets rather than logic.
- **Simple state machine for boss phases** (enum + switch, or a lightweight FSM) — avoid over-engineering with a full behavior tree for a 2-phase boss.
- **Event-driven damage/health** (C# events or UnityEvents) so UI, sound, and game logic don't need direct references to each other.

**Suggested Claude Code workflow:**
- Build and test one system at a time (e.g., "implement PlayerController movement + jump" → test in-editor → "add WeaponBase + SwordWeapon" → test → etc.)
- Keep prompts scoped to one script or one feature per session where possible; this matches the phased build order below and keeps generated code easy to review.

### 6.1 Commit Convention (required)

**Every commit message must use this form — no exceptions:**

```
<type>/ <short imperative description>
```

Example: `feat/ add dodge function with i-frames`

Rules:
- Type first, then a trailing slash, then a **space** — no colon.
- Description is lowercase, imperative mood ("add", not "added"/"adds"), and describes the change, not the file.
- Keep the subject line under ~72 characters. Longer explanation goes in the body after a blank line — that's the place for *why*, and for the Build Order step the commit belongs to.

Allowed types:

| Type | Use for |
|---|---|
| `feat/` | New gameplay system, mechanic, weapon, enemy, level, or UI screen |
| `fix/` | Bug fix — wrong behaviour, broken references, crashes |
| `refactor/` | Restructuring with no behaviour change |
| `art/` | Sprites, animations, 3D background assets, VFX, audio |
| `tune/` | Balance/feel-only changes: ScriptableObject values, timings, curves |
| `docs/` | This spec, README, code comments |
| `chore/` | Project settings, packages, .gitignore, meta housekeeping |

Notes:
- `tune/` exists separately from `feat/` on purpose — this project lives or dies on combat feel, and being able to `git log --grep '^tune/'` to find every balance change is worth the extra type.
- Unity scene/prefab/`.meta` churn rides along with the commit that caused it; it doesn't get its own `chore/`.
- Commits made before this convention was adopted are left as-is — do not rewrite history to match. The convention was previously written `/feat …` with a leading slash; commits in that form stay as they are, so a full history search needs both patterns: `git log --grep '^/\?tune'`.

---

## 7. Build Order (maps to semester timeline)

1. **Movement + camera follow** — get basic platforming feeling right.
2. **Sword combat vs. one dummy enemy** — the foundation. Don't move on until this feels good.
3. **Dodge (i-frames) tuned against a real enemy attack.**
4. **Health/damage system + HUD.**
5. **Sickle weapon** — should feel meaningfully different, not a reskin.
6. **Bow weapon** — aiming/projectile logic, resource cost.
7. **Specials (2)** — offensive burst, then mobility.
8. **Enemy types A and B**, then optional C.
9. **Level greybox → art pass**, level by level.
10. **3D background layer system.**
11. **Boss fight (2 phases).**
12. ~~**Story/dialogue integration.**~~ — **DEFERRED 2026-07-31, skip this step.** System is built; content is unwritten and blocked on §9's motivation question. Full note in §3.2.
13. **UI/menus (main menu, pause, game over, win screen).**
14. **Playtesting pass (4–5 people, start to finish).**
15. **Polish:** juice (hit-stop, screen shake, particles), audio, bug fixing.
16. **Build export + buffer.**

---

## 8. Explicit Cut List (do not add without cutting something else)

- No more than 3 weapons, 2 specials, 3 enemy types, 1 boss.
- No branching story paths — linear only.
- No interactive 3D gameplay — 3D is background/atmosphere only.
- No procedural level generation.
- Parry mechanic is a stretch goal only, added after dodge-based combat is solid — not a launch requirement.

---

## 9. Open Questions (to resolve during prototyping)
- ~~Final decision: dodge-only vs. dodge+parry?~~ **Resolved 2026-07-30: dodge + parry.** See §2.3.
- Resource system for bow/specials: shared "Focus" meter, or separate stamina/mana?
- Story delivery: text boxes, voice-lite (grunts/sfx + text), or full dialogue UI with portraits?
- Art medium for 2D sprites: pixel art vs. hand-drawn/vector — both can support the ukiyo-e direction, but affect workload differently (pixel art may be faster solo; vector/flat-shaded may match the woodblock look more directly).
- Final title: confirm "Hazakura" or one of the listed alternates.
- Exact protagonist backstory/motivation (revenge vs. redemption vs. searching for someone) — needed before writing story beats. **This is what Build Order step 12 is blocked on (deferred 2026-07-31, see §3.2); answering it is what unblocks it.**
