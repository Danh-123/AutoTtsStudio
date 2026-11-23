import { EdgeTTS } from '@andresaya/edge-tts';
import { writeFile } from 'node:fs/promises';
import { readFileSync, existsSync } from 'node:fs';

function parseArgs() {
  const args = process.argv.slice(2);
  const params = {};
  for (let i = 0; i < args.length; i++) {
    const key = args[i];
    if (key.startsWith('--')) {
      const name = key.slice(2);
      if (i + 1 < args.length && !args[i + 1].startsWith('--')) {
        params[name] = args[++i];
      } else {
        params[name] = true;
      }
    }
  }
  return params;
}

async function main() {
  const params = parseArgs();

  // If list voices requested
  if (params['list-voices']) {
    try {
      const tts = new EdgeTTS();
      const voices = await tts.getVoices();
      console.log(JSON.stringify(voices));
      process.exit(0);
    } catch (err) {
      console.error(err.message || String(err));
      process.exit(1);
    }
  }

  // Get text (either from file or direct string)
  let text = params.text || '';
  if (params['text-file'] && existsSync(params['text-file'])) {
    text = readFileSync(params['text-file'], 'utf-8');
  }

  if (!text) {
    console.error('Error: No text provided.');
    process.exit(1);
  }

  const voice = params.voice || 'en-US-AndrewMultilingualNeural';
  const outputPath = params.output;
  if (!outputPath) {
    console.error('Error: No output path provided.');
    process.exit(1);
  }

  // Options
  const options = {};
  if (params.rate) options.rate = params.rate; // e.g. "+0%" or "-10%"
  if (params.pitch) options.pitch = params.pitch; // e.g. "+0Hz"
  if (params.volume) options.volume = params.volume;

  try {
    const tts = new EdgeTTS(params.proxy ? { proxy: params.proxy } : undefined);
    const { audio, duration } = await tts.generate(text, voice, options);

    if (!audio || audio.length === 0) {
      console.error('Error: Generated audio is empty.');
      process.exit(1);
    }

    await writeFile(outputPath, audio);
    console.log(`SUCCESS: ${audio.length} bytes written, duration: ${duration}ms`);
    process.exit(0);
  } catch (err) {
    console.error(`ERROR: ${err.message || String(err)}`);
    process.exit(1);
  }
}

main();
