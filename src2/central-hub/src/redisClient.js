const { createClient } = require('redis');

const REDIS_URL = 'redis://localhost:6379';

const client = createClient({ url: REDIS_URL });

client.on('error', (err) => console.error('Redis Client Error', err));

async function connectRedis() {
    await client.connect();
    console.log('Connected to Redis.');
}

// Channels for Pub/Sub
const CHANNELS = {
    STATE_UPDATES: 'state_updates',
    CONTROL_COMMANDS: 'control_commands'
};

module.exports = {
    redisClient: client,
    connectRedis,
    CHANNELS
};
