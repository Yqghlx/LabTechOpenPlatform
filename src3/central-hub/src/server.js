const express = require('express');
const { garnetClient, connectGarnet, CHANNELS } = require('./garnetClient');

const app = express();
const PORT = process.env.PORT || 3001; // Use port 3001 to avoid conflict

app.use(express.json());

// --- API to get latest status ---
app.get('/api/systems/:systemId/status', async (req, res) => {
    const { systemId } = req.params;
    const key = `system:${systemId}`;
    try {
        const state = await garnetClient.get(key);
        if (state) {
            res.json(JSON.parse(state));
        } else {
            res.status(404).json({ error: 'System not found or no state received yet.' });
        }
    } catch (error) {
        res.status(500).json({ error: 'Failed to fetch system status from Garnet.' });
    }
});

// --- API to send control command ---
app.post('/api/systems/:systemId/control', async (req, res) => {
    const { systemId } = req.params;
    const command = req.body;

    if (!command || !command.action) {
        return res.status(400).json({ error: 'Invalid command format. "action" is required.' });
    }

    try {
        const message = JSON.stringify({ ...command, systemId });
        // Publish command to a channel specific to the systemId
        await garnetClient.publish(`${CHANNELS.CONTROL_COMMANDS}:${systemId}`, message);
        console.log(`Sent command '${command.action}' to channel for '${systemId}'`);
        res.status(202).json({ message: 'Command sent.' });
    } catch (error) {
        res.status(500).json({ error: 'Failed to send command via Garnet.' });
    }
});

// --- Subscriber to listen for all state updates ---
async function setupStateListener() {
    const subscriber = garnetClient.duplicate();
    await subscriber.connect();

    await subscriber.subscribe(CHANNELS.STATE_UPDATES, (message) => {
        try {
            const state = JSON.parse(message);
            const key = `system:${state.systemId}`;
            // Store the latest state in Garnet. The TTL (Time-To-Live) of 1 hour cleans up stale systems.
            garnetClient.set(key, JSON.stringify(state), { EX: 3600 });
            console.log(`Processed state update for: ${state.systemId}`);
        } catch (err) {
            console.error('Error processing state update:', err);
        }
    });
    console.log(`Listening for state updates on channel: ${CHANNELS.STATE_UPDATES}`);
}

async function startServer() {
    await connectGarnet();
    await setupStateListener();

    app.listen(PORT, () => {
        console.log(`Garnet-Only Central Hub listening on port ${PORT}`);
    });
}

startServer();
