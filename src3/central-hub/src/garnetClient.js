const { createClient } = require('redis'); // The 'redis' client works with Garnet due to RESP protocol compatibility.

const GARNET_URL = 'redis://localhost:3278'; // Default Garnet port is 3278

const client = createClient({ url: GARNET_URL });

client.on('error', (err) => console.error('Garnet Client Error', err));

async function connectGarnet() {
    await client.connect();
    console.log('Connected to Garnet.');
}

// Channels for Pub/Sub remain the same
const CHANNELS = {
    STATE_UPDATES: 'state_updates',
    CONTROL_COMMANDS: 'control_commands'
};

module.exports = {
    garnetClient: client,
    connectGarnet,
    CHANNELS
};
