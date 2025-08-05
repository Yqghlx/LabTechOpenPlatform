const express = require('express');
const { connect: connectMQ } = require('./services/messageQueue');
const { initPostgres, connectRedis } = require('./services/database');
const { listenForStateUpdates } = require('./services/stateProcessor');
const systemsApi = require('./api/systems');

const app = express();
const PORT = process.env.PORT || 3000;

app.use(express.json());

// --- API Routes ---
app.use('/api/systems', systemsApi);

async function startServer() {
    // 1. Connect to databases
    await initPostgres();
    await connectRedis();

    // 2. Connect to message queue
    await connectMQ();

    // 3. Start listening for state updates
    listenForStateUpdates();

    app.listen(PORT, () => {
        console.log(`Central Hub listening on port ${PORT}`);
    });
}

startServer();
