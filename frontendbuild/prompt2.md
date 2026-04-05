Prompt: Dashboard Styling & UI Layout Implement a clean, intuitive, and highly readable styling system for the EquiLink React dashboard using Tailwind CSS. Because EquiLink is a high-throughput trading gateway, the UI must prioritize data density and immediate visual comprehension over flashy animations .
Apply specific styling rules to the three core components:

    Live L2 Order Book Display: Style this as a compact data grid. Use distinct but accessible colors (e.g., subtle greens for bids, soft reds for asks) to clearly differentiate the spread sides .
    Real-Time Order Status Panel: Style this as a clean, auto-scrolling table. Implement pill-shaped status badges with semantic colors (e.g., Yellow for Pending, Blue for PartiallyFilled, Green for Filled, Red for Cancelled/Rejected) so operators can instantly read the order states .
    Risk Limit Utilisation Gauges: Style these as simple, horizontal progress bars or circular gauges. They must dynamically shift color from green to yellow, and finally to red as the specific fund approaches its dynamically configured risk limits (like Max Order Size or Daily Loss Limit) .

Ensure the overall layout uses a dark mode or high-contrast theme, which is standard for financial monitoring tools, keeping the layout strictly structural and easy to understand at a glance.
