# FreeTrain Plugin Translation Context

FreeTrain EX Av is a Japanese open source simulation game inspired by the A-Train series. The current project is porting it to a modern cross-platform C# and Avalonia codebase while preserving original behavior. Plugin manifests contain user-facing names and descriptions for trains, roads, stations, structures, land tiles, and other game objects.

Translate Japanese strings into natural English for in-game UI. Keep the translation faithful and compact. Do not embellish beyond the source.

## Output Requirements

- Return JSON only.
- Preserve every item `id` exactly.
- Return an array named `translations`.
- Each item must have `id` and `translation`.
- Do not include Markdown code fences.
- Leave model numbers, train class names, and contribution identifiers recognizable.

## Style

- Translate for a railway simulation UI, not for marketing copy.
- Prefer clear English names that fit menus and selection lists.
- Use sentence case for descriptions.
- Use title case sparingly for plugin or category names when it reads naturally.
- Keep punctuation simple and ASCII where possible.
- Preserve a trailing `*` if the source has one.
- Preserve parenthetical direction markers by translating them.

## Railway Terms

- `系` after a rolling stock number usually means `Series`, for example `E231系` -> `E231 Series`.
- `先頭車` -> `lead car`.
- `中間車` -> `intermediate car`.
- `制御車` -> `control car`.
- `電動車` -> `motor car`.
- `付随車` -> `trailer car`.
- `前` -> `front`.
- `後` -> `rear`.
- `上り` -> `inbound` only when clearly describing rail direction; otherwise use context.
- `下り` -> `outbound` only when clearly describing rail direction; otherwise use context.
- `普通` -> `local`.
- `快速` -> `rapid`.
- `急行` -> `express`.
- `特急` -> `limited express`.
- `新幹線` -> `Shinkansen`.
- `貨車` -> `freight car`.
- `客車` -> `passenger coach`.
- `気動車` -> `diesel railcar`.
- `電車` -> `electric train` unless a rolling-stock technical label is clearly better.

## Naming Rules

- Preserve proper railway operator names where possible: JR East, JR Central, JR West, JNR, Keio, Keikyu, Keisei, Meitetsu, Hankyu, Hanshin, Tokyu, Tobu, Seibu, Sotetsu, Tokyo Metro.
- Romanize fictional names when a literal translation would sound strange.
- Keep Japanese car classification names such as `クハ`, `モハ`, `サハ`, `キハ`, `キロ`, `オハ`, `スハ`, `ワキ`, `タキ`, `コキ` as romanized class codes when they are part of a vehicle name.
- For geographic names, use common English names where well-known: Tokyo, Kyoto, Osaka, Yokohama, Nagoya, Sendai.

## Common Structure Terms

- `駅` -> `station`.
- `ホーム` -> `platform`.
- `道路` -> `road`.
- `橋` -> `bridge`.
- `高架` -> `elevated`.
- `地下` -> `underground`.
- `ビル` -> `building`.
- `マンション` -> `apartment building`.
- `工場` -> `factory`.
- `倉庫` -> `warehouse`.
- `住宅` -> `house` or `residential building`, depending on context.
- `田` -> `rice field`.
- `森` -> `forest`.

## Ambiguity

If a string is a compact asset name and context is insufficient, prefer a safe literal translation over guessing. If it appears to be a model designation, preserve it.

## More Rolling Stock Guidance

Many plugin strings are short railway labels assembled from company, series, and role. Translate these consistently so menus sort and scan well.

- `国鉄` -> `JNR`.
- `旧国鉄` or `旧日本国有鉄道` -> `former JNR`.
- `JR貨物` -> `JR Freight`.
- `JR東日本` -> `JR East`.
- `JR東海` -> `JR Central`.
- `JR西日本` -> `JR West`.
- `JR北海道` -> `JR Hokkaido`.
- `JR四国` -> `JR Shikoku`.
- `JR九州` -> `JR Kyushu`.
- `私鉄` -> `private railway`.
- `地下鉄` -> `subway`.
- `モノレール` -> `monorail`.
- `路面電車` -> `tram`.
- `市電` -> `municipal tram`.
- `バス` -> `bus`.
- `市営バス` -> `municipal bus`.
- `貨物` -> `freight`.
- `コンテナ` -> `container`.
- `タンク` -> `tank`.
- `郵便` -> `mail`.
- `荷物` -> `baggage`.
- `寝台` -> `sleeper`.
- `グリーン車` -> `Green Car`.
- `食堂車` -> `dining car`.
- `展望車` -> `observation car`.
- `機関車` -> `locomotive`.
- `電気機関車` -> `electric locomotive`.
- `ディーゼル機関車` -> `diesel locomotive`.
- `蒸気機関車` -> `steam locomotive`.

## More Infrastructure Guidance

- `駅舎` -> `station building`.
- `車庫` -> `depot`.
- `機関庫` -> `engine shed`.
- `留置線` -> `storage track`.
- `踏切` -> `level crossing`.
- `信号` -> `signal`.
- `架線柱` -> `catenary pole`.
- `電柱` -> `utility pole`.
- `街灯` -> `streetlight`.
- `防音壁` -> `sound barrier`.
- `柵` -> `fence`.
- `擁壁` -> `retaining wall`.
- `トンネル` -> `tunnel`.
- `地下駅` -> `underground station`.
- `港` -> `port`.
- `空港` -> `airport`.
- `野球場` -> `baseball stadium`.
- `サッカースタジアム` -> `soccer stadium`.
- `テニスコート` -> `tennis court`.
- `学校` -> `school`.
- `病院` -> `hospital`.
- `公園` -> `park`.
- `寺` -> `temple`.
- `神社` -> `shrine`.
- `墓地` -> `cemetery`.
- `駐車場` -> `parking lot`.
- `商店` -> `shop`.
- `デパート` -> `department store`.
- `ショッピングセンター` -> `shopping center`.
- `高層ビル` -> `high-rise building`.
- `団地` -> `housing complex`.
- `アパート` -> `apartment building`.

## Descriptions

Some descriptions are historical notes about real trains. Keep dates, numbers, speeds, and names intact. Translate era names into readable English when practical, but do not over-explain them. If the source uses an informal or uncertain tone, keep the meaning but make the English UI-friendly.
