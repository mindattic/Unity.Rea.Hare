# Credits and third-party assets (U.HARE-T69)

Everything here is third-party material that ships in, or is rasterised into, the Unity.Hare rebuild of the GameMaker
game HARE (`gmlHARE\HARE.gmx`). The same list is shown in game: the title's **Credits** link (bottom right, once the
menu is up) opens it (`Assets/Unity.Hare/Scripts/UI/CreditsView.cs`). Keep the two in step.

The original game had no credits room and its only credit file is `datafiles/credits.txt`; the in-game screen is a
recorded deviation (`docs/BIBLE.md` V52).

Licences are only stated here when a file in this repo or in the original project says so. Where none does, the entry
is **unverified** and the source of the claim is named. Nothing below has been looked up online.

## Sound effects

| Asset | Author | Source | Licence |
|---|---|---|---|
| Minigun sound effect (which clip(s) it became is not recorded; the vulcan sounds are the likely ones) | Jim Rogers | http://soundbible.com/1920-Minigun.html, from the original's `datafiles/credits.txt` (kept as `Assets/Unity.Hare/Audio/credits.txt`) | unverified: the credit file gives no licence |
| The sound effects (69 `sfx_*` clips under `Assets/Unity.Hare/Audio/Sounds`) apart from whatever the minigun credit covers | unverified | `HARE.gmx\sound\audio`; no credit or licence recorded anywhere in the original | unverified |

## Music (`Assets/Unity.Hare/Audio/Sounds/music`)

Author and title come from each file's ID3 tag. No tag or file states a licence; the tags on four say the album is
"Royalty Free", which is not a licence grant by itself.

| Track | Author | Source | Licence |
|---|---|---|---|
| In a Heartbeat | Kevin MacLeod | ID3 of `music_inaheartbeat.mp3` (2006) | unverified |
| Latin Industries | Kevin MacLeod | ID3 of `music_latinindustries.mp3` (2012) | unverified ("Royalty Free" tag) |
| Overcast | Kevin MacLeod | ID3 of `music_overcast.mp3` (2012) | unverified ("Royalty Free" tag) |
| Pinball Spring | unverified | file name only; `music_pinballspring.mp3` has no author tag | unverified |
| Presenterator | Kevin MacLeod | ID3 of `music_presenterator.mp3` (2011) | unverified ("Royalty Free" tag) |
| Wallpaper | Kevin MacLeod | ID3 of `music_wallpaper.mp3` (2011) | unverified ("Royalty Free" tag) |

Before release: confirm the actual licence of each track with its author's site (if it is an attribution licence, this
list is the attribution) and identify the author of Pinball Spring.

## Fonts

From `Assets/Unity.Hare/Fonts/gml-fonts.json` and `docs/BIBLE.md` section 6.

| Face | Owner | Licence | What ships |
|---|---|---|---|
| Arial | Monotype, licensed to Microsoft (ships with Windows) | proprietary; use on the machine it came with, no redistribution of the font file | no font file; only GameMaker's rasterised glyph atlases (`fnt_basic`, `font4`, `font7`, `font8`) |
| Berlin Sans FB | The Font Bureau, licensed to Microsoft (ships with Office) | proprietary; no redistribution of the font file | no font file; only GameMaker's rasterised glyph atlases (`fnt_medium`, `fnt_point`, `fnt_score`, `fnt_small`) |

`GmlFontPipelineTests` fails if a `.ttf` or `.otf` appears under `Assets`. Whether publishing rasterisations of these
two proprietary faces is permitted is **not settled by any file here**; before a public release either get that
confirmed or swap in open faces (every screen follows through `HareTextStyles`).

## Sprites and backgrounds

All art comes from `HARE.gmx` (`Assets/Unity.Hare/Art`). The original project records no author, source or licence for
any sprite, background or splash image: **unverified**. Before release, establish who drew each, or replace the ones
nobody can vouch for.

## Open items before any release

1. Licence and author of every sound effect other than the minigun, and the minigun's own licence terms.
2. Licence of the five Kevin MacLeod tracks; author and licence of Pinball Spring.
3. Whether the proprietary fonts' rasterisations may be published.
4. Provenance of all sprites and backgrounds.
