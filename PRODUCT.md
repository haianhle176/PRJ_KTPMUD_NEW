# Product

## Register

product

## Users

OmniConnect is primarily for SmartHome-style users who want to connect, monitor, and control simulated IoT devices without understanding raw MQTT topics, device IDs, capability keys, or database state.

The user is usually in a task-focused context: checking whether devices are online, reading current sensor values, turning actuators on or off, reviewing notifications, and creating simple If-Then automation rules.

Secondary users include lecturers and reviewers who need to see that the platform works end-to-end, and developers who may need Advanced views for debugging.

## Product Purpose

OmniConnect is a dynamic IoT platform that lets devices declare their own capabilities at runtime. Users claim devices, name and classify them, view telemetry, send actuator commands, receive notifications, and create automation rules.

Success means the interface feels like a real SmartHome control app: simple on the surface, technically capable underneath, and clear enough to demo without Swagger, Postman, or database inspection.

## Brand Personality

Calm, clear, capable.

The product should feel trustworthy and modern, but not flashy. It should make complex IoT behavior feel understandable: device state, sensor readings, actuator controls, automation, and warnings should be visible without overwhelming the user.

## Anti-references

Do not make the app look like a raw admin panel, Swagger replacement, database browser, or developer debug console in the primary experience.

Avoid AI-slop UI patterns:

- Decorative gradient text.
- Glassmorphism as default.
- Identical card grids that do not reflect user tasks.
- Hero-metric templates used as decoration.
- Colored side-stripe cards or alerts.
- Overly futuristic dashboards that make common SmartHome tasks feel technical.
- Exposing raw IDs, capability keys, command names, or bitmasks in the normal user flow.

Technical views are allowed, but they belong in Advanced mode.

## Design Principles

1. Make the simple path obvious: common SmartHome actions should be visible within seconds.
2. Hide technical complexity until it is needed: normal users see names, values, toggles, and rules; Advanced users can inspect raw telemetry and command details.
3. Treat status as the product: online/offline, latest values, command state, notifications, and automation outcomes must be easy to read.
4. Prefer earned familiarity over novelty: use standard product UI patterns that feel reliable and task-focused.
5. Preserve demo credibility: the UI should help explain the platform without looking like a prototype.

## Accessibility & Inclusion

Target WCAG AA for text contrast, focus visibility, and keyboard navigation.

Every interactive control should have visible labels, accessible focus states, and clear disabled/loading/error/success states where relevant. Motion should be purposeful, short, and safe to reduce or remove for users who prefer reduced motion.
