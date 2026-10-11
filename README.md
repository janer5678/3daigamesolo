# 3daigamesolo

A first-person parkour game in Unity 6 (6000.4.0f1), styled after the Pururin world from J's World: flat pastel cel shading and ink outlines.

**Levels** (Tab switches between them):

- **Akiba** (`Assets/Scenes/Akiba.unity`, the game opens here): a few blocks of Akihabara at night, everything wearing AI-generated photographs on low-poly shapes (the Mallbat look; `Tools/gen_akiba_photos.py` runs Stable Diffusion XL locally, repeating textures made seamless by repainting the seam), drawn a little rough in a PlayStation style. One short avenue (240 m), a canyon of neon buildings with a plaza in the middle, and floating over the middle of it two dozen chunks of the city torn loose (bits of road, pavement, shrine gardens, rooftops) at every height from 7 to 58 m, the highest a shrine with a giant lucky cat. It's built so there's always a way from wherever you are to anywhere else, some easy and some needing skill: escalators (one floats up to an island), glass lifts up the four towers, walkways along the fronts joined by footbridges with moving walkways, launch pads (a trail of dots shows which way they throw you, and you stick the landing), vents, trampolines, short rails, gaps to jump, fast buses on the avenue with a kicker on the roof (get on from the raised bus stops), slow flying buses that stop at sky stops, islands and a tower top (ride on the roof), the train across the north end, and flying cars: land on one and you fly it for five seconds (steer with the mouse, W/S speed, Space up, C down) before it falls apart and throws you on. The builder records every surface and every way between them (`Assets/Editor/SkateWorldBuilder.AkibaLinks.cs`), makes sure every place can be reached and has ways on, and the test runs ride every one of those ways. Built by `Assets/Editor/SkateWorldBuilder.Akiba.cs`.
- **Aero** (`Assets/Scenes/Aero.unity`): Frutiger Aero. Bright green rolling hills under a blue sky with sun rays and fluffy clouds, soap bubbles and big tropical fish floating in the air, a glass city on the horizon; you're a glossy blue jelly person riding a CD, with a glossy Aero HUD (a player card with a level and XP earned by skating, a clock, bars for speed, height and air). A long valley rolls down from the starting hill (you can hit 120 km/h) and narrows into a grassy halfpipe; glowing rings shoot you on (two angle up towards the clouds); bubbles bounce you, and a staircase of them climbs to clouds you can skate on (rainbow rails on top); the fish swim loops you can ride on their backs; there's a lake you can skate across; and the glass city has a glass hill to ride over, a ramp spiralling up its tallest tower, and a plaza of quarter pipes, funboxes, ledges and rails. Built by `Assets/Editor/SkateWorldBuilder.Aero.cs` (shaders `SkateGlossy`, `SkateBubble`, `SkateRays`).
- **Infinity** (`Assets/Scenes/Infinity.unity`): after a fly-through of a 3D fractal. A great tower whose top is a plaza with four arms flaring up into curved banks; off its corners, bridges run down to four towers half its size that do the same, four generations deep (53 towers), copied all round you into the dark and upside down overhead so it seems to go on for ever, everything glowing in one colour that slowly cycles. Ride the banks, drop down the bridges from generation to generation (grind their edges), and from the tip of every smallest tower a launch pad throws you back up to the top. Built by `Assets/Editor/SkateWorldBuilder.Infinity.cs` (shader `SkateFractal`).
- **Sakura City** (`Assets/Scenes/SakuraCity.unity`): a small Japanese city in the same pastel toon style, lighter. A ring road with cars and buses going round (ride on their roofs), an elevated railway over the main boulevard with a train running back and forth through a station, power lines on poles all round the ring and up to rooftops, guard rails, handrails and rooftop edges to grind, shop streets with awnings, vending machines and alleys, a parking garage to skate up, a skate plaza, cherry blossom drifting about, flickering signs and spinning rooftop fans. The goal is the top of the tower in the middle (58 m), and there's more than one way up: skate up the garage and grind the power line to the 22 m roof beside the tower, or take the train route (station stairs, the train's roof, the canopy, across the rooftops); then climb a billboard frame onto the tower's terrace (30 m), and the staggered balconies to the top. Built by `Assets/Editor/SkateWorldBuilder.City.cs`.
- **Pink Palace** (`Assets/Scenes/PinkPalace.unity`): a Y2K pink palace on a floating island in deep space. Glittery halftone tiles, a grand staircase up to a terrace, a fountain, ramps, ledges, giant strawberries, floating hearts and a ringed planet. Run out through a gap in the railing and you fall into space and start again. Off the west side, over a bridge, is **the tower**: a spiral ramp (skate it) round a pink core to 14 m, a wall-jump shaft to 30 m, another to 46 m, then stepping stones spiralling up to a crown at 56 m (the wall-jump shafts are left over from when you could wall-jump), with a checkpoint at each level. Out of the gap on the east side runs the **space course**: floating platforms, stepping stones and pillars climbing to a goal under a giant star, with checkpoints (hearts on poles) along the way. Built by `Assets/Editor/SkateWorldBuilder.Palace.cs`.
- **Pururin Valley** (`Assets/Scenes/SkatePark.unity`): the pastel valley, a long run ending in a plaza with ramps, ledges and a big curved wall. The ground is currently flat (`FlatGround` in `SkateWorldBuilder.cs`; set it to false and rebuild for the downhill valley).

Rails, power lines and ledges can be ground on the skateboard.

## Controls

First person (press **V** for third person). Gravity is much heavier than Unity's default, and heavier still on the way down, so jumps are quick and don't hang.

| Key | |
|---|---|
| **Mouse** | look |
| **WASD** | run (about 16 km/h); you start, stop and turn almost instantly, and have nearly full control in the air |
| **Shift** | sprint (about 27 km/h); on the board, go faster |
| **Space** | jump: tap for a short one, **hold for a higher one**. Coyote time (you can still jump just after running off an edge) and jump buffering (press just before landing and you jump on touchdown). Hopping again and again the same way slowly builds speed. Pressing into a wall while falling slides you down it slowly |
| **C** | slide: a burst of speed, keeps your momentum, and you fit under low things (barriers, bars); jump out of it to go higher. On the board it's a powerslide: carve hard without losing speed, and come out of it faster |
| **Skateboard** | it's always with you: you start each level riding it. Without Shift you cruise (about 25 km/h); hold Shift and you go faster (about 50 km/h). Kickers launch you. Hold Shift on a rail to power grind (full speed, even uphill). You stay on it however you land. **Right click** (or Q) steps off, carrying it, for running and wall jumps; right click again to drop back on. Jump onto a rail or power line on the board to grind it (A/D switches grind; Space jumps off). Only really big air kickflips it, and only sometimes |
| **V** | switch between first and third person |
| **Tab** | next level |
| **R** | back to the last checkpoint (or the start) |
| **Esc / click** | free / recapture the mouse |

Tuning lives on the Player object's `SlideController`, grouped into Look, Third person, Running, Jumping, Slide, Air, Skateboard and Rails.

## Rebuilding the world

The scenes are generated by `Assets/Editor/SkateWorldBuilder.cs` and `SkateWorldBuilder.Palace.cs` (menu **Skate > Build world**). Change the layout there and rebuild rather than editing the scenes by hand. **Skate > Run test runs** steps the real controller and physics through scripted runs (running and stopping, jump height and air control, held jumps, coyote time, jump buffering, hop speed, sliding, solid ground everywhere, the palace stairs) and logs PASS/FAIL to the console.

Command line:

```
Unity -batchmode -projectPath . -executeMethod SkateWorldBuilder.BuildFromCommandLine   # build both levels + tests + screenshots in Review/
Unity -batchmode -projectPath . -executeMethod SkateWorldBuilder.BuildPlayer            # Windows build in Builds/Staging
```

The shaders in `Assets/Shaders` are adapted from J's World's Pururin world shaders. The HUD font is Bangers (SIL Open Font License, see `Assets/Resources/Bangers - OFL.txt`).
