const { createClient } = require('redis'); // The 'redis' client works with Garnet due to RESP protocol compatibility.

// --- Configuration ---
const SYSTEM_ID = process.env.SYSTEM_ID || 'system-A-garnet'; 
const GARNET_URL = 'redis://localhost:3278'; // Default Garnet port

const CHANNELS = {
    STATE_UPDATES: 'state_updates',
    CONTROL_COMMANDS: 'control_commands'
};

async function main() {
    const publisher = createClient({ url: GARNET_URL });
    const subscriber = publisher.duplicate();

    try {
        await Promise.all([publisher.connect(), subscriber.connect()]);
        console.log(`[${SYSTEM_ID}] Connected to Garnet.`);

        // --- 1. State Publishing Logic ---
        const publishState = () => {
            const state = {
                systemId: SYSTEM_ID,
                timestamp: new Date().toISOString(),
                status: 'online',
                metrics: {
                    cpu: Math.random().toFixed(2),
                    mem: Math.random().toFixed(2),
                }
            };
            publisher.publish(CHANNELS.STATE_UPDATES, JSON.stringify(state));
            console.log(`[${SYSTEM_ID}] Sent state update.`);
        };

        // Publish state every 5 seconds
        setInterval(publishState, 5000);

        // --- 2. Control Command Listening Logic ---
        const controlChannel = `${CHANNELS.CONTROL_COMMANDS}:${SYSTEM_ID}`;
        subscriber.subscribe(controlChannel, (message) => {
            const command = JSON.parse(message);
            console.log(`[${SYSTEM_ID}] Received command:`, command);
            // TODO: Add your logic here to control the actual system.
        });
        console.log(`[${SYSTEM_ID}] Subscribed to command channel: ${controlChannel}`);

    } catch (error) {
        console.error(`[${SYSTEM_ID}] Adapter failed:`, error);
        process.exit(1);
    }
}

main();
