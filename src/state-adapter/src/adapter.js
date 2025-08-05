const amqp = require('amqplib');

// --- Configuration ---
// IMPORTANT: Change this for each system you deploy this adapter to.
const SYSTEM_ID = process.env.SYSTEM_ID || 'system-A'; 

const RABBITMQ_URL = 'amqp://user:password@localhost:5672';
const STATE_UPDATE_QUEUE = 'state_updates';
const CONTROL_COMMANDS_EXCHANGE = 'control_commands';

async function main() {
    let channel;
    try {
        const connection = await amqp.connect(RABBITMQ_URL);
        channel = await connection.createChannel();

        // --- State Publishing Logic ---
        // This function simulates getting state from your actual system.
        // In a real scenario, you would replace this with calls to your system's API, DB, etc.
        const publishState = () => {
            const state = {
                systemId: SYSTEM_ID,
                timestamp: new Date().toISOString(),
                status: 'online',
                metrics: {
                    cpuUsage: Math.random().toFixed(2),
                    memoryUsage: Math.random().toFixed(2),
                    activeTasks: Math.floor(Math.random() * 100)
                }
            };

            channel.sendToQueue(STATE_UPDATE_QUEUE, Buffer.from(JSON.stringify(state)), { persistent: true });
            console.log(`[${SYSTEM_ID}] Sent state update:`, state.metrics);
        };

        // Publish state every 5 seconds
        setInterval(publishState, 5000);

        // --- Control Command Listening Logic ---
        await channel.assertExchange(CONTROL_COMMANDS_EXCHANGE, 'direct', { durable: false });
        const q = await channel.assertQueue('', { exclusive: true });

        // Bind the queue to the exchange with our systemId as the routing key
        channel.bindQueue(q.queue, CONTROL_COMMANDS_EXCHANGE, SYSTEM_ID);

        console.log(`[${SYSTEM_ID}] Waiting for control commands...`);
        channel.consume(q.queue, (msg) => {
            if (msg.content) {
                const command = JSON.parse(msg.content.toString());
                console.log(`[${SYSTEM_ID}] Received command:`, command);
                //
                // TODO: Add your logic here to control the actual system.
                // For example: if (command.action === 'reboot') { ... }
                //
            }
        }, { noAck: true });

    } catch (error) {
        console.error(`[${SYSTEM_ID}] Adapter failed:`, error);
        // Retry connection after a delay
        setTimeout(main, 10000);
    }
}

main();
