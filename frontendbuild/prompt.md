Prompt: React Real-Time Monitoring Dashboard Initialize a React application (using Vite and TypeScript) to serve as the internal monitoring dashboard for the EquiLink trading gateway. Install the @microsoft/signalr package for WebSocket connectivity.
Build the frontend with three core components:

    Live L2 Order Book Display: A visual representation of the real-time market data feed.
    Real-Time Order Status Panel: A table or panel that dynamically updates order states (e.g., Pending, PartiallyFilled, Filled) as async execution reports arrive.
    Risk Limit Utilization Gauges: Visual indicators showing how close a specific fund is to hitting its dynamically configured risk limits.

Integration Constraints:

    Configure the REST API client to connect to https://localhost:7001. Ensure the client is configured to accept Brotli compression, as the backend API optimizes dashboard responses with it.
    Configure the SignalR client to connect to the market data hub at wss://localhost:7001/hubs/marketdata.
    Crucial SignalR Subscription Strategy: The frontend must not receive a broadcast of the entire market. Implement a mechanism where the SignalR client explicitly subscribes to specific symbol groups (e.g., 'AAPL', 'MSFT') to receive targeted L2 updates.