# Original indoor handball audio

All six `HKA_Handball/Resources/Raw/Sounds/*.wav` effects are original procedural
renders, with no third-party recordings, sampled music, downloads or additional
dependencies. There is no existing music. The generator is supplied under the
repository's terms; no external CC0/license claim is made.

Regenerate from the repository root with `node tools/generate-sounds.mjs`.
Validate the actual assets with `node tools/generate-sounds.mjs --check`.
Add `--audition <output.wav>` to either command for a listening sampler in this
order: click, pass twice, shoot, goal, whistle, crowd (0.6-second gaps).
The whistle sampler uses the existing runtime gain of 0.35.

| Asset | Physical design | Existing callers |
| --- | --- | --- |
| `click.wav` | Quiet tactile wooden table tap | Menu difficulty, mode and team-colour selection |
| `pass.wav` | Soft handball shell/palm contact and surface friction | Pass, AwayPass |
| `shoot.wav` | Harder handball contact and short air swish | Shoot, AwayShoot |
| `goal.wav` | Ball/net impact, irregular cord flutter, two diminishing timber-floor bounces | GoalHome, GoalAway |
| `whistle.wav` | Air-driven inharmonic whistle cavities, turbulent breath and irregular pea flutter | Whistle, HalfTime, FullTime, PenaltyAwarded, Suspension unless a penalty is active |
| `crowd.wav` | Layered asynchronous palm claps, modest indoor applause | Existing `PlayCrowd()` API; no current gameplay caller |

Save and Interception currently emit events but have no audio caller. There is
no dribble or post-hit event/asset. This redesign preserves those semantics
rather than pretending such sounds were integrated or adding new gameplay.

Every effect uses band-limited excitation, damped resonant modes where
appropriate, early sporthall reflections (19/37/61/89 ms), and a damped diffuse
tail (~0.52-second reverberation decay). The UI tap has less room sound. Files
are mono 44.1 kHz signed 16-bit PCM for Android/Windows compatibility, with DC
removal, 1 ms entry fades and 60 ms final fades. Frequent passes/clicks have
lower peaks than shots/goals. The whistle remains at runtime gain 0.35.
The whole set is approximately 0.55 MiB, loaded locally through the unchanged
MAUI raw-asset glob and logical `Sounds/<name>.wav` paths.

The checker enforces the exact asset/preload inventory, RIFF/PCM sizes and format, duration,
no clipped samples, peak headroom, non-silence, DC and endpoint bounds, a final
20 ms RMS below -58 dBFS, practical loudest-50-ms level bounds for repeated
effects and whistle gain, room impulse reflections/diffuse returns, and
byte-exact reproducibility (including reflections).
Objective checks cannot certify perceived realism; listen to the sampler and
test on device speakers/headphones before claiming human-audition approval.
