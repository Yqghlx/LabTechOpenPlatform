const { Pool } = require('pg');
const { createClient } = require('redis');

// --- PostgreSQL Configuration ---
const pool = new Pool({
    user: 'user',
    host: 'localhost',
    database: 'labtech',
    password: 'password',
    port: 5432,
});

async function initPostgres() {
    const client = await pool.connect();
    try {
        await client.query(`
            CREATE TABLE IF NOT EXISTS system_states (
                id SERIAL PRIMARY KEY,
                system_id VARCHAR(100) NOT NULL,
                status VARCHAR(50),
                metrics JSONB,
                timestamp TIMESTAMPTZ NOT NULL,
                created_at TIMESTAMPTZ DEFAULT NOW()
            );
        `);
        console.log('PostgreSQL table initialized.');
    } finally {
        client.release();
    }
}

// --- Redis Configuration ---
const redisClient = createClient({ url: 'redis://localhost:6379' });

redisClient.on('error', (err) => console.error('Redis Client Error', err));

async function connectRedis() {
    await redisClient.connect();
    console.log('Connected to Redis.');
}

module.exports = {
    pgPool: pool,
    redisClient,
    initPostgres,
    connectRedis
};
