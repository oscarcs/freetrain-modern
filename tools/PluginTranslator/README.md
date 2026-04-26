# Plugin Translator

This tool deduplicates `plugins/**/plugin.en.json` source strings, sends unique untranslated strings to Gemini, and applies the returned translations back into every sidecar file.

## Setup

```bash
cd tools/PluginTranslator
npm install
cp .env.example .env
```

Put your Gemini key in `.env`:

```bash
GEMINI_API_KEY=...
```

## Workflow

From `tools/PluginTranslator`:

```bash
npm run dedupe
npm run cache
npm run translate -- --limit 1
npm run apply -- --dry-run
npm run apply
npm run status
```

`dedupe` scans all sidecars, deduplicates by exact Japanese source string, and writes `state/dedupe.json` plus `state/batches/*.json`.

`cache` uploads `context.md` and creates a Gemini cached-content record. The cache name is saved in `state/gemini-cache.json`. Reuse it for all batch calls while it remains valid.

`translate` sends pending batches to Gemini and stores accepted translations in `state/translations.json`. Use `--limit 1` for the first paid smoke test.

`apply` fills matching blank `translation` fields in every `plugin.en.json`. It also propagates one translated source string to all duplicate occurrences.

## Useful Options

```bash
npm run dedupe -- --batch-items 120 --batch-chars 30000
npm run cache -- --model gemini-3-flash-preview
npm run translate -- --model gemini-3-flash-preview --start 4 --limit 2
npm run apply -- --overwrite
```

Use the same `--model` for `cache` and `translate`, because Gemini caches are model-specific.
