const { getChannel, STATE_UPDATE_QUEUE } = require('./messageQueue');
const { pgPool, redisClient } = require('./database');

function listenForStateUpdates() {
    const channel = getChannel();

    channel.consume(STATE_UPDATE_QUEUE, async (msg) => {
        if (msg !== null) {
            try {
                const state = JSON.parse(msg.content.toString());
                console.log(`Received state update for: ${state.systemId}`);

                // 1. Store in PostgreSQL (for history)
                await pgPool.query(
                    'INSERT INTO system_states (system_id, status, metrics, timestamp) VALUES ($1, $2, $3, $4)',
                    [state.systemId, state.status, JSON.stringify(state.metrics), state.timestamp]
                );

                // 2. Cache in Redis (for latest state)
                const redisKey = `system:${state.systemId}`;
                await redisClient.set(redisKey, JSON.stringify(state));

                channel.ack(msg);
            } catch (error) {
                console.error('Failed to process state update', error);
                channel.nack(msg, false, false); // Don't requeue poison messages
            }
        }
    });
}

module.exports = { listenForStateUpdates };
