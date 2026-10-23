class FmodOutputProcessor extends AudioWorkletProcessor {
    constructor(options) {
        super();
        const { memory, pointer } = options.processorOptions;
        this.words = new Int32Array(memory, pointer, 8);
        this.capacity = this.words[6];
        this.channels = this.words[7];
        this.samples = new Float32Array(memory, pointer + 32, this.capacity * this.channels);
        this.closed = false;
        this.port.onmessage = (event) => {
            if (event.data !== "close" || this.closed) return;
            this.closed = true;
            this.port.postMessage("closed");
        };
    }
    process(inputs, outputs) {
        if (this.closed) return false;
        const output = outputs[0];
        if (!output.length || !Atomics.load(this.words, 2)) return true;
        const frames = output[0].length;
        const read = Atomics.load(this.words, 1) >>> 0;
        const write = Atomics.load(this.words, 0) >>> 0;
        const available = Math.min(frames, (write - read) >>> 0);
        for (let frame = 0; frame < available; frame++) {
            const start = ((read + frame) & (this.capacity - 1)) * this.channels;
            for (let channel = 0; channel < output.length; channel++)
                output[channel][frame] = this.samples[start + channel];
        }
        // The browser provides zeroed output buffers. Unavailable frames remain silence.
        Atomics.store(this.words, 1, (read + available) | 0);
        Atomics.add(this.words, 3, 1);
        Atomics.notify(this.words, 3, 1);
        return true;
    }
}
registerProcessor("celeste-fmod-output", FmodOutputProcessor);
