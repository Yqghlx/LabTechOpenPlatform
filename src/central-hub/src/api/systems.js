const express = require('express');
const { redisClient } = require('../services/database');
const { getChannel, CONTROL_COMMANDS_EXCHANGE } = require('../services/messageQueue');

const router = express.Router();

// GET /api/systems/:systemId/status - Get latest status for a system
router.get('/:systemId/status', async (req, res) => {
    const { systemId } = req.params;
    const redisKey = `system:${systemId}`;

    try {
        const state = await redisClient.get(redisKey);
        if (state) {
            res.json(JSON.parse(state));
        } else {
            res.status(404).json({ error: 'System not found or no state received yet.' });
        }
    } catch (error) {
        res.status(500).json({ error: 'Failed to fetch system status.' });
    }
});

// POST /api/systems/:systemId/control - Send a control command
router.post('/:systemId/control', (req, res) => {
    const { systemId } = req.params;
    const command = req.body;

    if (!command || !command.action) {
        return res.status(400).json({ error: 'Invalid command format. "action" is required.' });
    }

    try {
        const channel = getChannel();
        const message = JSON.stringify({ ...command, systemId });
        
        // Publish to the exchange with the systemId as routing key
        channel.publish(CONTROL_COMMANDS_EXCHANGE, systemId, Buffer.from(message));
        
        console.log(`Sent command '${command.action}' to system '${systemId}'`);
        res.status(202).json({ message: 'Command sent.' });
    } catch (error) {
        res.status(500).json({ error: 'Failed to send command.' });
    }
});

module.exports = router;
