import fs from "node:fs/promises";
import path from "node:path";
import crypto from "node:crypto";
import { fileURLToPath } from "node:url";

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(scriptDir, "../..");
const defaultPluginRoot = path.join(repoRoot, "plugins");
const defaultStateRoot = path.join(scriptDir, "state");
const defaultContextPath = path.join(scriptDir, "context.md");
const defaultModel = "gemini-3-flash-preview";
const defaultBatchItemLimit = 180;
const defaultBatchCharLimit = 45_000;

const command = process.argv[2] ?? "status";
const options = parseArgs(process.argv.slice(3));

try {
  switch (command) {
    case "prepare":
      await prepare(options);
      break;
    case "cache":
      await createCache(options);
      break;
    case "translate":
      await translate(options);
      break;
    case "apply":
      await applyTranslations(options);
      break;
    case "status":
      await status(options);
      break;
    case "help":
    case "--help":
    case "-h":
      printHelp();
      break;
    default:
      throw new Error(`Unknown command: ${command}`);
  }
} catch (error) {
  console.error(error instanceof Error ? error.message : error);
  process.exitCode = 1;
}

async function prepare(options) {
  const pluginRoot = resolvePath(options.plugins ?? defaultPluginRoot);
  const stateRoot = resolvePath(options.state ?? defaultStateRoot);
  const batchItemLimit = Number(options["batch-items"] ?? defaultBatchItemLimit);
  const batchCharLimit = Number(options["batch-chars"] ?? defaultBatchCharLimit);

  const sidecars = await readSidecars(pluginRoot);
  const existingBySource = collectExistingTranslations(sidecars);
  const translationStore = await readTranslationStore(stateRoot);

  const itemsBySource = new Map();
  let totalEntries = 0;

  for (const sidecar of sidecars) {
    for (const text of sidecar.data.texts ?? []) {
      if (!text?.source || !text?.key) {
        continue;
      }

      totalEntries++;
      const id = sourceId(text.source);
      let item = itemsBySource.get(text.source);
      if (!item) {
        item = {
          id,
          source: text.source,
          count: 0,
          examples: []
        };
        itemsBySource.set(text.source, item);
      }

      item.count++;
      if (item.examples.length < 5) {
        item.examples.push({
          plugin: sidecar.data.plugin || path.basename(path.dirname(sidecar.path)),
          key: text.key,
          field: fieldName(text.key)
        });
      }
    }
  }

  const allItems = [...itemsBySource.values()].sort((a, b) =>
    b.count - a.count || a.source.localeCompare(b.source, "ja"));

  const pendingItems = allItems.filter((item) =>
    !existingBySource.has(item.source)
    && !hasStoredTranslation(translationStore, item.id, item.source));

  await fs.rm(path.join(stateRoot, "batches"), { recursive: true, force: true });
  await fs.mkdir(path.join(stateRoot, "batches"), { recursive: true });

  const batches = splitBatches(pendingItems, batchItemLimit, batchCharLimit);
  for (let i = 0; i < batches.length; i++) {
    await writeJson(path.join(stateRoot, "batches", batchName(i)), {
      batch: i,
      itemCount: batches[i].length,
      items: batches[i]
    });
  }

  await writeJson(path.join(stateRoot, "dedupe.json"), {
    generatedAt: new Date().toISOString(),
    pluginRoot,
    sidecarCount: sidecars.length,
    totalEntries,
    uniqueSources: allItems.length,
    existingTranslatedSources: existingBySource.size,
    storedTranslatedSources: countStoredTranslations(translationStore),
    pendingSources: pendingItems.length,
    batchCount: batches.length,
    items: allItems
  });

  console.log(`Sidecars: ${sidecars.length}`);
  console.log(`Entries: ${totalEntries}`);
  console.log(`Unique source strings: ${allItems.length}`);
  console.log(`Pending unique strings: ${pendingItems.length}`);
  console.log(`Batches written: ${batches.length}`);
}

async function createCache(options) {
  const stateRoot = resolvePath(options.state ?? defaultStateRoot);
  const contextPath = resolvePath(options.context ?? defaultContextPath);
  const model = String(options.model ?? defaultModel);
  const env = await readEnv(options.env);
  const apiKey = options["api-key"] ?? env.GEMINI_API_KEY;
  if (!apiKey) {
    throw new Error("Missing GEMINI_API_KEY. Put it in tools/PluginTranslator/.env or pass --api-key.");
  }

  const { GoogleGenAI, createUserContent, createPartFromUri } = await import("@google/genai");
  const ai = new GoogleGenAI({ apiKey });

  const doc = await ai.files.upload({
    file: contextPath,
    config: { mimeType: "text/markdown" }
  });

  const cache = await ai.caches.create({
    model,
    config: {
      contents: createUserContent(createPartFromUri(doc.uri, doc.mimeType)),
      systemInstruction: "You are a meticulous Japanese-to-English localization translator for a railway simulation game. Follow the uploaded translation context exactly."
    }
  });

  await fs.mkdir(stateRoot, { recursive: true });
  await writeJson(path.join(stateRoot, "gemini-cache.json"), {
    createdAt: new Date().toISOString(),
    model,
    contextPath,
    file: {
      name: doc.name,
      uri: doc.uri,
      mimeType: doc.mimeType
    },
    cache
  });

  console.log(`Uploaded context file: ${doc.name}`);
  console.log(`Created cache: ${cache.name}`);
}

async function translate(options) {
  const stateRoot = resolvePath(options.state ?? defaultStateRoot);
  const model = String(options.model ?? (await readCacheInfo(stateRoot))?.model ?? defaultModel);
  const batchLimit = Number(options.limit ?? Number.POSITIVE_INFINITY);
  const startBatch = Number(options.start ?? 0);
  const dryRun = Boolean(options["dry-run"]);
  const translationStore = await readTranslationStore(stateRoot);
  const batches = await listBatches(stateRoot);
  const pendingBatches = [];
  for (const batchPath of batches) {
    const batch = await readJson(batchPath);
    if (batch.batch < startBatch) {
      continue;
    }

    const pendingItems = batch.items.filter((item) =>
      !hasStoredTranslation(translationStore, item.id, item.source));
    if (pendingItems.length > 0) {
      pendingBatches.push({ ...batch, path: batchPath, items: pendingItems });
    }
  }

  console.log(`Pending batches: ${pendingBatches.length}`);
  if (dryRun) {
    for (const batch of pendingBatches.slice(0, Number.isFinite(batchLimit) ? batchLimit : pendingBatches.length)) {
      console.log(`Batch ${batch.batch}: ${batch.items.length} items`);
    }
    return;
  }

  const env = await readEnv(options.env);
  const apiKey = options["api-key"] ?? env.GEMINI_API_KEY;
  if (!apiKey) {
    throw new Error("Missing GEMINI_API_KEY. Put it in tools/PluginTranslator/.env or pass --api-key.");
  }

  const cacheInfo = await readCacheInfo(stateRoot);
  if (!cacheInfo?.cache?.name) {
    throw new Error("No Gemini cache found. Run `npm run cache` first.");
  }

  const { GoogleGenAI } = await import("@google/genai");
  const ai = new GoogleGenAI({ apiKey });
  let translatedBatches = 0;

  for (const batch of pendingBatches) {
    if (translatedBatches >= batchLimit) {
      break;
    }

    const prompt = buildBatchPrompt(batch);
    const response = await ai.models.generateContent({
      model,
      contents: prompt,
      config: {
        cachedContent: cacheInfo.cache.name,
        responseMimeType: "application/json",
        temperature: Number(options.temperature ?? 0.2)
      }
    });

    const text = response.text ?? "";
    await fs.mkdir(path.join(stateRoot, "responses"), { recursive: true });
    await fs.writeFile(path.join(stateRoot, "responses", `batch-${String(batch.batch).padStart(3, "0")}.json.txt`), text);

    const parsed = parseJsonResponse(text);
    const translations = parsed.translations ?? [];
    let accepted = 0;
    for (const translated of translations) {
      if (!translated?.id || typeof translated.translation !== "string") {
        continue;
      }

      const sourceItem = batch.items.find((item) => item.id === translated.id);
      if (!sourceItem || translated.translation.trim().length === 0) {
        continue;
      }

      translationStore.translations[translated.id] = {
        source: sourceItem.source,
        translation: translated.translation.trim(),
        model,
        batch: batch.batch,
        updatedAt: new Date().toISOString()
      };
      accepted++;
    }

    translationStore.updatedAt = new Date().toISOString();
    await writeTranslationStore(stateRoot, translationStore);
    translatedBatches++;
    console.log(`Batch ${batch.batch}: accepted ${accepted}/${batch.items.length} translations`);
  }
}

async function applyTranslations(options) {
  const pluginRoot = resolvePath(options.plugins ?? defaultPluginRoot);
  const stateRoot = resolvePath(options.state ?? defaultStateRoot);
  const overwrite = Boolean(options.overwrite);
  const dryRun = Boolean(options["dry-run"]);
  const sidecars = await readSidecars(pluginRoot);
  const translationStore = await readTranslationStore(stateRoot);
  const translationsBySource = collectExistingTranslations(sidecars);

  for (const [id, stored] of Object.entries(translationStore.translations ?? {})) {
    if (stored?.source && stored?.translation) {
      translationsBySource.set(stored.source, stored.translation);
    }
  }

  let updatedFiles = 0;
  let updatedEntries = 0;

  for (const sidecar of sidecars) {
    let changed = false;
    for (const text of sidecar.data.texts ?? []) {
      if (!text?.source) {
        continue;
      }

      if (!overwrite && typeof text.translation === "string" && text.translation.trim().length > 0) {
        continue;
      }

      const translation = translationsBySource.get(text.source);
      if (translation && text.translation !== translation) {
        text.translation = translation;
        changed = true;
        updatedEntries++;
      }
    }

    if (changed) {
      updatedFiles++;
      if (!dryRun) {
        await writeJson(sidecar.path, sidecar.data);
      }
    }
  }

  console.log(`Files ${dryRun ? "would update" : "updated"}: ${updatedFiles}`);
  console.log(`Entries ${dryRun ? "would fill" : "filled"}: ${updatedEntries}`);
}

async function status(options) {
  const pluginRoot = resolvePath(options.plugins ?? defaultPluginRoot);
  const stateRoot = resolvePath(options.state ?? defaultStateRoot);
  const sidecars = await readSidecars(pluginRoot);
  const translationStore = await readTranslationStore(stateRoot);
  const cacheInfo = await readCacheInfo(stateRoot);
  let entries = 0;
  let filled = 0;
  const uniqueSources = new Set();
  const filledSources = new Set();

  for (const sidecar of sidecars) {
    for (const text of sidecar.data.texts ?? []) {
      entries++;
      uniqueSources.add(text.source);
      if (text.translation?.trim()) {
        filled++;
        filledSources.add(text.source);
      }
    }
  }

  console.log(`Sidecars: ${sidecars.length}`);
  console.log(`Entries: ${filled}/${entries} filled`);
  console.log(`Unique sources: ${filledSources.size}/${uniqueSources.size} filled`);
  console.log(`Stored translations: ${countStoredTranslations(translationStore)}`);
  console.log(`Gemini cache: ${cacheInfo?.cache?.name ?? "not created"}`);
}

function buildBatchPrompt(batch) {
  const compactItems = batch.items.map((item) => ({
    id: item.id,
    source: item.source,
    count: item.count,
    examples: item.examples
  }));

  return [
    "Translate this batch of FreeTrain plugin strings from Japanese to English.",
    "Return JSON only in this exact shape:",
    "{\"translations\":[{\"id\":\"same-id\",\"translation\":\"English translation\"}]}",
    "Translate every item exactly once. Preserve ids exactly.",
    JSON.stringify({ batch: batch.batch, items: compactItems })
  ].join("\n\n");
}

function splitBatches(items, itemLimit, charLimit) {
  const batches = [];
  let current = [];
  let currentChars = 0;

  for (const item of items) {
    const itemChars = item.source.length + JSON.stringify(item.examples).length + 64;
    if (current.length > 0 && (current.length >= itemLimit || currentChars + itemChars > charLimit)) {
      batches.push(current);
      current = [];
      currentChars = 0;
    }

    current.push(item);
    currentChars += itemChars;
  }

  if (current.length > 0) {
    batches.push(current);
  }

  return batches;
}

async function readSidecars(pluginRoot) {
  const files = await findFiles(pluginRoot, "plugin.en.json");
  const sidecars = [];
  for (const file of files) {
    const data = await readJson(file);
    if (Array.isArray(data.texts)) {
      sidecars.push({ path: file, data });
    }
  }

  return sidecars;
}

async function findFiles(root, fileName) {
  const results = [];
  async function visit(dir) {
    const entries = await fs.readdir(dir, { withFileTypes: true });
    for (const entry of entries) {
      const fullPath = path.join(dir, entry.name);
      if (entry.isDirectory()) {
        await visit(fullPath);
      } else if (entry.isFile() && entry.name === fileName) {
        results.push(fullPath);
      }
    }
  }

  await visit(root);
  return results.sort((a, b) => a.localeCompare(b));
}

function collectExistingTranslations(sidecars) {
  const translations = new Map();
  for (const sidecar of sidecars) {
    for (const text of sidecar.data.texts ?? []) {
      if (text?.source && typeof text.translation === "string" && text.translation.trim()) {
        translations.set(text.source, text.translation.trim());
      }
    }
  }

  return translations;
}

async function readTranslationStore(stateRoot) {
  const storePath = path.join(stateRoot, "translations.json");
  try {
    return await readJson(storePath);
  } catch (error) {
    if (error.code !== "ENOENT") {
      throw error;
    }
  }

  return {
    updatedAt: null,
    translations: {}
  };
}

async function writeTranslationStore(stateRoot, store) {
  await fs.mkdir(stateRoot, { recursive: true });
  await writeJson(path.join(stateRoot, "translations.json"), store);
}

async function readCacheInfo(stateRoot) {
  try {
    return await readJson(path.join(stateRoot, "gemini-cache.json"));
  } catch (error) {
    if (error.code === "ENOENT") {
      return null;
    }

    throw error;
  }
}

async function listBatches(stateRoot) {
  const batchRoot = path.join(stateRoot, "batches");
  const entries = await fs.readdir(batchRoot);
  return entries
    .filter((entry) => /^batch-\d+\.json$/.test(entry))
    .sort()
    .map((entry) => path.join(batchRoot, entry));
}

function hasStoredTranslation(store, id, source) {
  const stored = store.translations?.[id];
  return Boolean(stored?.translation?.trim() && stored.source === source);
}

function countStoredTranslations(store) {
  return Object.values(store.translations ?? {})
    .filter((item) => item?.source && item?.translation?.trim())
    .length;
}

function sourceId(source) {
  return crypto.createHash("sha256").update(source, "utf8").digest("hex").slice(0, 16);
}

function fieldName(key) {
  const match = key.match(/\.([^.]+)$/);
  return match ? match[1] : key;
}

function batchName(index) {
  return `batch-${String(index).padStart(3, "0")}.json`;
}

async function readEnv(explicitPath) {
  const envPath = resolvePath(explicitPath ?? path.join(scriptDir, ".env"));
  const values = { ...process.env };
  let text;
  try {
    text = await fs.readFile(envPath, "utf8");
  } catch (error) {
    if (error.code === "ENOENT") {
      return values;
    }

    throw error;
  }

  for (const line of text.split(/\r?\n/)) {
    const trimmed = line.trim();
    if (!trimmed || trimmed.startsWith("#")) {
      continue;
    }

    const equals = trimmed.indexOf("=");
    if (equals < 0) {
      continue;
    }

    const key = trimmed.slice(0, equals).trim();
    let value = trimmed.slice(equals + 1).trim();
    if ((value.startsWith("\"") && value.endsWith("\"")) || (value.startsWith("'") && value.endsWith("'"))) {
      value = value.slice(1, -1);
    }

    values[key] = value;
  }

  return values;
}

function parseJsonResponse(text) {
  const trimmed = text.trim();
  try {
    return JSON.parse(trimmed);
  } catch {
    const fenced = trimmed.match(/```(?:json)?\s*([\s\S]*?)```/i);
    if (fenced) {
      return JSON.parse(fenced[1]);
    }

    const firstBrace = trimmed.indexOf("{");
    const lastBrace = trimmed.lastIndexOf("}");
    if (firstBrace >= 0 && lastBrace > firstBrace) {
      return JSON.parse(trimmed.slice(firstBrace, lastBrace + 1));
    }

    throw new Error("Gemini response did not contain parseable JSON.");
  }
}

async function readJson(filePath) {
  return JSON.parse(await fs.readFile(filePath, "utf8"));
}

async function writeJson(filePath, value) {
  await fs.mkdir(path.dirname(filePath), { recursive: true });
  await fs.writeFile(filePath, `${JSON.stringify(value, null, 2)}\n`, "utf8");
}

function resolvePath(candidate) {
  return path.isAbsolute(String(candidate))
    ? String(candidate)
    : path.resolve(process.cwd(), String(candidate));
}

function parseArgs(args) {
  const parsed = {};
  for (let i = 0; i < args.length; i++) {
    const arg = args[i];
    if (!arg.startsWith("--")) {
      parsed._ = [...(parsed._ ?? []), arg];
      continue;
    }

    const withoutPrefix = arg.slice(2);
    const equals = withoutPrefix.indexOf("=");
    if (equals >= 0) {
      parsed[withoutPrefix.slice(0, equals)] = withoutPrefix.slice(equals + 1);
      continue;
    }

    const key = withoutPrefix;
    const next = args[i + 1];
    if (next && !next.startsWith("--")) {
      parsed[key] = next;
      i++;
    } else {
      parsed[key] = true;
    }
  }

  return parsed;
}

function printHelp() {
  console.log(`FreeTrain plugin translator

Commands:
  prepare     Deduplicate plugin.en.json source strings and write state/batches
  cache       Upload context.md and create a Gemini cached content record
  translate   Translate pending batches through Gemini
  apply       Fill plugin.en.json translation fields from stored translations
  status      Show sidecar and translation progress

Common options:
  --plugins <path>       Plugin root, defaults to ../../plugins from this tool
  --state <path>         State directory, defaults to tools/PluginTranslator/state
  --env <path>           .env file, defaults to tools/PluginTranslator/.env
  --model <name>         Gemini model, defaults to ${defaultModel}

Examples:
  npm install
  npm run prepare
  npm run cache
  npm run translate -- --limit 1
  npm run apply -- --dry-run
  npm run apply
`);
}
