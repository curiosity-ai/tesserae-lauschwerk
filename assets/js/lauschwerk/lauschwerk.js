// Written by Tesserae.Lauschwerk's build/bundle-lauschwerk.mjs - do not edit.
import * as lauschwerk from './v0.3.2/index.js';

const base = new URL('./v0.3.2/', import.meta.url).href;
lauschwerk.Lauschwerk.configure({ assetBaseUrl: base });

globalThis.lauschwerk = Object.freeze(Object.assign({}, lauschwerk, { version: '0.3.2', assetBaseUrl: base, engineSha256: '2cfbed5620c22ad6e9f334a41f3f8c226808cb3cb1dad1d06b570d76691a9a65' }));
