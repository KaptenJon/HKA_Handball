import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import crypto from 'node:crypto';

// Original, deterministic physical sound models. No recordings or external packages.
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const output = path.join(root, 'HKA_Handball', 'Resources', 'Raw', 'Sounds');
const rate = 44100;
const specs = {
    click: { duration: 0.32, peak: 0.24, seed: 17 },
    pass: { duration: 0.56, peak: 0.34, seed: 29 },
    shoot: { duration: 0.68, peak: 0.46, seed: 43 },
    goal: { duration: 1.05, peak: 0.52, seed: 71 },
    whistle: { duration: 1.28, peak: 0.68, seed: 101 },
    crowd: { duration: 2.65, peak: 0.43, seed: 137 },
};

function random(seed) {
    return () => {
        seed |= 0;
        seed = seed + 0x6D2B79F5 | 0;
        let n = Math.imul(seed ^ seed >>> 15, 1 | seed);
        n = n + Math.imul(n ^ n >>> 7, 61 | n) ^ n;
        return ((n ^ n >>> 14) >>> 0) / 4294967296;
    };
}

function noise(length, rng, cutoff = 6500) {
    const result = new Float64Array(length);
    const alpha = 1 - Math.exp(-2 * Math.PI * cutoff / rate);
    let low = 0;
    let dc = 0;
    for (let i = 0; i < length; i++) {
        low += alpha * ((rng() * 2 - 1) - low);
        dc += 0.004 * (low - dc);
        result[i] = low - dc;
    }
    return result;
}

function add(target, source, seconds, gain = 1) {
    const start = Math.round(seconds * rate);
    for (let i = 0; i < source.length && start + i < target.length; i++)
        target[start + i] += source[i] * gain;
}

function burst(duration, rng, cutoff, decay, attack = 0.0008) {
    const result = noise(Math.ceil(duration * rate), rng, cutoff);
    for (let i = 0; i < result.length; i++) {
        const t = i / rate;
        result[i] *= (1 - Math.exp(-t / attack)) * Math.exp(-t / decay);
    }
    return result;
}

// A contact impulse excites damped shell/panel modes, rather than playing bare tones.
function contact(rng, strength, soft = false) {
    const length = Math.round(rate * 0.22);
    const result = burst(0.22, rng, soft ? 2400 : 5200, soft ? 0.013 : 0.007);
    const excitation = burst(0.009, rng, 2400, 0.002);
    const modes = soft
        ? [[146, 0.036, 0.65], [273, 0.024, 0.27], [481, 0.015, 0.12]]
        : [[173, 0.048, 0.72], [326, 0.031, 0.34], [612, 0.017, 0.16], [1083, 0.010, 0.08]];
    for (const [frequency, decay, gain] of modes) {
        const r = Math.exp(-1 / (rate * decay));
        const a = 2 * r * Math.cos(2 * Math.PI * frequency / rate);
        const b = r * r;
        let previous = 0;
        let before = 0;
        for (let i = 0; i < length; i++) {
            const next = (excitation[i] ?? 0) * 0.09 + a * previous - b * before;
            result[i] += next * gain;
            before = previous;
            previous = next;
        }
    }
    for (let i = 0; i < length; i++) result[i] *= strength;
    return result;
}

// Shared small sporthall: discrete wall/floor reflections and damped diffuse returns.
function hall(dry, wet = 0.22) {
    const result = Float64Array.from(dry);
    for (const [delay, gain] of [[0.019, 0.33], [0.037, -0.24], [0.061, 0.19], [0.089, 0.13]]) {
        const offset = Math.round(delay * rate);
        let low = 0;
        for (let i = offset; i < result.length; i++) {
            low += 0.32 * (dry[i - offset] - low);
            result[i] += wet * gain * low;
        }
    }
    for (const delay of [0.0437, 0.0593, 0.0719, 0.0839]) {
        const offset = Math.round(delay * rate);
        const line = new Float64Array(offset);
        const feedback = Math.pow(0.001, delay / 0.52);
        let low = 0;
        for (let i = 0; i < result.length; i++) {
            const index = i % offset;
            low += 0.24 * (line[index] - low);
            line[index] = dry[i] + low * feedback;
            result[i] += low * wet * 0.24;
        }
    }
    return result;
}

function make(name, spec) {
    const rng = random(spec.seed);
    const dry = new Float64Array(Math.round(spec.duration * rate));
    if (name === 'click') {
        // Quiet, tactile tap on a wooden scorer's table.
        add(dry, burst(0.08, rng, 4200, 0.003), 0.003, 0.6);
        add(dry, contact(rng, 0.18, true), 0.004);
    } else if (name === 'pass') {
        // Palm release and leather/rubber catch, less bass and bite than a shot.
        add(dry, burst(0.055, rng, 1800, 0.010, 0.003), 0.004, 0.2);
        add(dry, contact(rng, 0.75, true), 0.016);
        add(dry, burst(0.10, rng, 3400, 0.021), 0.025, 0.12);
    } else if (name === 'shoot') {
        // Hard hand-to-ball contact with a short broadband arm/ball swish.
        add(dry, contact(rng, 1.0), 0.006);
        add(dry, burst(0.12, rng, 4200, 0.028, 0.007), 0.009, 0.28);
    } else if (name === 'goal') {
        // Ball flexing net cords, followed by irregular slack-net flutter and floor landing.
        add(dry, contact(rng, 0.50, true), 0.006);
        add(dry, burst(0.18, rng, 4700, 0.047, 0.002), 0.010, 0.65);
        for (const [time, gain] of [[0.049, 0.24], [0.087, 0.19], [0.132, 0.12], [0.187, 0.07]])
            add(dry, burst(0.08, rng, 3500, 0.018), time, gain);
        add(dry, contact(rng, 0.48), 0.255);
        add(dry, contact(rng, 0.16), 0.416);
    } else if (name === 'whistle') {
        // Air jet drives three inharmonic whistle cavity modes; turbulence and
        // irregular pea modulation prevent a clean electronic notification tone.
        const air = noise(dry.length, rng, 7600);
        const phases = [0, 1.7, 3.4];
        const frequencies = [2860, 3427, 4093];
        let flutter = 0;
        let pitchNoise = 0;
        for (let i = 0; i < dry.length; i++) {
            const t = i / rate;
            const blow = (1 - Math.exp(-Math.max(0, t - 0.014) / 0.009))
                * Math.min(1, Math.max(0, (0.49 - t) / 0.060));
            flutter += 0.045 * (rng() * 2 - 1 - flutter);
            pitchNoise += 0.012 * (rng() * 2 - 1 - pitchNoise);
            const pea = 0.73 + 0.19 * Math.sin(2 * Math.PI * 47 * t + flutter * 5);
            let cavity = 0;
            for (let j = 0; j < phases.length; j++) {
                phases[j] += 2 * Math.PI * frequencies[j] * (1 + 0.002 * pitchNoise) / rate;
                cavity += Math.sin(phases[j]) * [0.55, 0.31, 0.12][j];
            }
            dry[i] = blow * (cavity * pea + air[i] * 0.15);
        }
    } else if (name === 'crowd') {
        // Modest indoor applause, not artificial synthesized voices or a stadium roar.
        for (let person = 0; person < 14; person++) {
            let time = 0.04 + rng() * 0.25;
            const spacing = 0.16 + rng() * 0.13;
            while (time < 1.82) {
                const gain = (0.18 + rng() * 0.32) * Math.min(1, (1.94 - time) / 0.6);
                add(dry, burst(0.085, rng, 3800 + rng() * 2100, 0.005 + rng() * 0.007), time, gain);
                add(dry, contact(rng, 0.045, true), time + 0.001, gain);
                time += spacing * (0.84 + rng() * 0.32);
            }
        }
    }
    const result = hall(dry, name === 'click' ? 0.10 : name === 'crowd' ? 0.38 : 0.27);
    let dc = 0;
    for (let i = 0; i < result.length; i++) {
        dc += 0.0015 * (result[i] - dc);
        const fadeIn = Math.min(1, i / (rate * 0.001));
        const fadeOut = Math.min(1, (result.length - 1 - i) / (rate * 0.060));
        result[i] = (result[i] - dc) * fadeIn * fadeOut;
    }
    const peak = result.reduce((max, sample) => Math.max(max, Math.abs(sample)), 0);
    for (let i = 0; i < result.length; i++) result[i] *= spec.peak / peak;
    return result;
}

function encode(samples) {
    const buffer = Buffer.alloc(44 + samples.length * 2);
    buffer.write('RIFF', 0);
    buffer.writeUInt32LE(buffer.length - 8, 4);
    buffer.write('WAVEfmt ', 8);
    buffer.writeUInt32LE(16, 16);
    buffer.writeUInt16LE(1, 20);
    buffer.writeUInt16LE(1, 22);
    buffer.writeUInt32LE(rate, 24);
    buffer.writeUInt32LE(rate * 2, 28);
    buffer.writeUInt16LE(2, 32);
    buffer.writeUInt16LE(16, 34);
    buffer.write('data', 36);
    buffer.writeUInt32LE(samples.length * 2, 40);
    for (let i = 0; i < samples.length; i++)
        buffer.writeInt16LE(Math.round(samples[i] * 32767), 44 + i * 2);
    return buffer;
}

function db(value) { return 20 * Math.log10(Math.max(value, 1 / 32768)); }

function check() {
    const impulse = new Float64Array(rate);
    impulse[0] = 1;
    const room = hall(impulse, 0.27);
    for (const delay of [0.019, 0.037, 0.061, 0.089])
        if (Math.abs(room[Math.round(delay * rate)]) < 0.001)
            throw new Error(`Missing sporthall early reflection at ${delay}s`);
    const lateEnergy = room.slice(Math.round(rate * 0.10), Math.round(rate * 0.30))
        .reduce((sum, sample) => sum + sample * sample, 0);
    if (lateEnergy < 0.00001) throw new Error('Missing diffuse sporthall return');
    const expected = Object.keys(specs).sort();
    const actual = fs.readdirSync(output).filter(name => name.endsWith('.wav')).map(name => name.slice(0, -4)).sort();
    if (JSON.stringify(expected) !== JSON.stringify(actual)) throw new Error(`Audio inventory mismatch: ${actual}`);
    const service = fs.readFileSync(path.join(root, 'HKA_Handball', 'Services', 'SoundManager.cs'), 'utf8');
    const preloadNames = [...service.match(/SoundNames\s*=\s*\[([^\]]+)\]/)[1].matchAll(/"([^"]+)"/g)]
        .map(match => match[1]).sort();
    if (JSON.stringify(expected) !== JSON.stringify(preloadNames)) throw new Error(`Preload coverage mismatch: ${preloadNames}`);
    const reports = [];
    for (const name of expected) {
        const bytes = fs.readFileSync(path.join(output, `${name}.wav`));
        const spec = specs[name];
        if (bytes.toString('ascii', 0, 4) !== 'RIFF' || bytes.toString('ascii', 8, 16) !== 'WAVEfmt '
            || bytes.readUInt32LE(4) !== bytes.length - 8 || bytes.readUInt16LE(20) !== 1
            || bytes.readUInt16LE(22) !== 1 || bytes.readUInt32LE(24) !== rate
            || bytes.readUInt16LE(34) !== 16 || bytes.toString('ascii', 36, 40) !== 'data'
            || bytes.readUInt32LE(40) !== bytes.length - 44)
            throw new Error(`${name}: invalid PCM WAV format/header`);
        const count = (bytes.length - 44) / 2;
        let peak = 0, sum = 0, mean = 0, clipped = 0, tail = 0, early = 0, reflected = 0;
        let windowSum = 0, windowPeak = 0;
        const windowLength = Math.round(rate * 0.050);
        for (let i = 0; i < count; i++) {
            const sample = bytes.readInt16LE(44 + i * 2) / 32768;
            peak = Math.max(peak, Math.abs(sample));
            sum += sample * sample;
            mean += sample;
            if (Math.abs(sample) >= 0.999) clipped++;
            if (i >= count - rate * 0.020) tail += sample * sample;
            if (i < rate * 0.25) early += sample * sample;
            if (i >= rate * 0.10 && i < rate * 0.24) reflected += sample * sample;
            windowSum += sample * sample;
            if (i >= windowLength) {
                const previous = bytes.readInt16LE(44 + (i - windowLength) * 2) / 32768;
                windowSum -= previous * previous;
            }
            windowPeak = Math.max(windowPeak, windowSum / windowLength);
        }
        const duration = count / rate;
        const tailDb = db(Math.sqrt(tail / (rate * 0.020)));
        const rmsDb = db(Math.sqrt(sum / count));
        if (Math.abs(duration - spec.duration) > 1 / rate || clipped || peak > 0.71 || peak < 0.20
            || rmsDb < -42 || tailDb > -58 || Math.abs(mean / count) > 0.001
            || bytes.readInt16LE(44) !== 0 || bytes.readInt16LE(bytes.length - 2) !== 0
            || reflected <= 0 || early <= 0)
            throw new Error(`${name}: signal contract failed (peak=${peak}, RMS=${rmsDb}, tail=${tailDb})`);
        // Byte-for-byte equality tests physical render + reflection/fade properties, not just headers.
        if (!bytes.equals(encode(make(name, spec)))) throw new Error(`${name}: differs from reproducible physical model`);
        reports.push({ name, duration, bytes: bytes.length, rate, channels: 1, bits: 16,
            peakDb: +db(peak).toFixed(2), rmsDb: +rmsDb.toFixed(2), tailDb: +tailDb.toFixed(2),
            loudest50msDb: +db(Math.sqrt(windowPeak)).toFixed(2),
            clipped, sha256: crypto.createHash('sha256').update(bytes).digest('hex') });
    }
    const loudness = Object.fromEntries(reports.map(report => [report.name, report.loudest50msDb]));
    if (loudness.click > loudness.pass || loudness.pass > loudness.shoot
        || loudness.whistle + db(0.35) > loudness.shoot + 3)
        throw new Error(`Frequent-effect/whistle loudness balance failed: ${JSON.stringify(loudness)}`);
    console.log(JSON.stringify(reports, null, 2));
}

if (process.argv.includes('--check')) {
    check();
} else {
    fs.mkdirSync(output, { recursive: true });
    for (const [name, spec] of Object.entries(specs))
        fs.writeFileSync(path.join(output, `${name}.wav`), encode(make(name, spec)));
    console.log(`Generated ${Object.keys(specs).length} original sporthall effects in ${output}`);
    check();
}

const auditionIndex = process.argv.indexOf('--audition');
if (auditionIndex >= 0) {
    const destination = process.argv[auditionIndex + 1];
    if (!destination) throw new Error('--audition requires an output WAV path');
    const sequence = ['click', 'pass', 'pass', 'shoot', 'goal', 'whistle', 'crowd'];
    const parts = sequence.map(name => {
        const samples = make(name, specs[name]);
        const gain = name === 'whistle' ? 0.35 : 1;
        return Float64Array.from(samples, sample => sample * gain);
    });
    const gap = Math.round(rate * 0.60);
    const sampler = new Float64Array(parts.reduce((sum, part) => sum + part.length + gap, gap));
    let offset = gap;
    for (const part of parts) {
        sampler.set(part, offset);
        offset += part.length + gap;
    }
    fs.mkdirSync(path.dirname(destination), { recursive: true });
    fs.writeFileSync(destination, encode(sampler));
    console.log(`Audition sequence ${sequence.join(', ')}: ${destination}`);
}
