# 3daigamesolo

A first-person parkour game in Unity 6 (6000.4.0f1), styled after the Pururin world from J's World: flat pastel cel shading and ink outlines.

**Levels** (Tab switches between them):

- **Akiba** (`Assets/Scenes/Akiba.unity`, the game opens here): an Akihabara night, everything wearing AI-generated photographs on low-poly shapes (the Mallbat look; `Tools/gen_akiba_photos.py` runs Stable Diffusion XL locally), drawn a little rough in a PlayStation style. A 960 m main street packed with neon-covered buildings, shop stalls and machines, slow traffic both ways (long buses you can get on, ramp trucks you ride up), a median strip of junk to skate (barriers, kickers, wedges, wrecked cars, pipes, funboxes), side streets strung with power lines and full of ramps, an elevated railway with Akihabara station and a train, skyscrapers up to 210 m with curvy rails spiralling right up them, and power lines between pylons over the top of it all. The curvy rails grab you from a couple of metres away and rush you along until you jump off. Flying cars go round on their own (ride their roofs), and rocket cars sit parked with a ramp on the back, in the street and on rooftops: ride up it and you're off, flat out wherever you look (Space up, C down, right click to jump off). After 4 seconds (or as soon as it hits something) it explodes and throws you up and onward; it turns up again where it was a few seconds later. Everything is meant to throw you on to the next thing: glowing arrows on the ground boost you, giant capsule toys and pink shop awnings bounce you, vents blow you up skyscraper fronts to their balconies and from roof to roof, rooftops are joined by ramps, footbridges cross the street with ramps up from the pavement and railings to grind, and the side streets have torii gates and lantern strings to grind, ramen carts and bike racks. The route: kick off the ramp onto the curvy rail, it takes you onto a balcony, ride the sign ramp to the roof, hop on the flying car, and as it climbs the skyscraper the pipe up its corner grabs you and takes you into the power lines at the top. Built by `Assets/Editor/SkateWorldBuilder.Akiba.cs`.
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
