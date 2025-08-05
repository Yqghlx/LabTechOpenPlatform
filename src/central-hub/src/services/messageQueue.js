const amqp = require('amqplib');

const RABBITMQ_URL = 'amqp://user:password@localhost:5672';
const STATE_UPDATE_QUEUE = 'state_updates';
const CONTROL_COMMANDS_EXCHANGE = 'control_commands';

let channel = null;

async function connect() {
    try {
        const connection = await amqp.connect(RABBITMQ_URL);
        channel = await connection.createChannel();
        
        await channel.assertQueue(STATE_UPDATE_QUEUE, { durable: true });
        await channel.assertExchange(CONTROL_COMMANDS_EXCHANGE, 'direct', { durable: false });

        console.log('Connected to RabbitMQ');
    } catch (error) {
        console.error('Failed to connect to RabbitMQ', error);
        process.exit(1);
    }
}

function getChannel() {
    if (!channel) {
        throw new Error('RabbitMQ channel is not available.');
    }
    return channel;
}

module.exports = { connect, getChannel, STATE_UPDATE_QUEUE, CONTROL_COMMANDS_EXCHANGE };
