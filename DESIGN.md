---
name: OmniConnect
description: Calm SmartHome control interface for a dynamic IoT platform.
colors:
  primary: "#2563EB"
  primary-hover: "#1D4ED8"
  primary-soft: "#DBEAFE"
  background: "#F8FAFC"
  surface: "#FFFFFF"
  surface-muted: "#F1F5F9"
  ink: "#0F172A"
  ink-muted: "#475569"
  border: "#CBD5E1"
  success: "#16A34A"
  warning: "#D97706"
  danger: "#DC2626"
  info: "#0284C7"
typography:
  display:
    fontFamily: "Segoe UI, Inter, system-ui, sans-serif"
    fontSize: "28px"
    fontWeight: 700
    lineHeight: 1.15
    letterSpacing: "-0.01em"
  headline:
    fontFamily: "Segoe UI, Inter, system-ui, sans-serif"
    fontSize: "22px"
    fontWeight: 650
    lineHeight: 1.2
  title:
    fontFamily: "Segoe UI, Inter, system-ui, sans-serif"
    fontSize: "16px"
    fontWeight: 600
    lineHeight: 1.3
  body:
    fontFamily: "Segoe UI, Inter, system-ui, sans-serif"
    fontSize: "14px"
    fontWeight: 400
    lineHeight: 1.45
  label:
    fontFamily: "Segoe UI, Inter, system-ui, sans-serif"
    fontSize: "12px"
    fontWeight: 600
    lineHeight: 1.25
rounded:
  sm: "6px"
  md: "10px"
  lg: "16px"
  xl: "24px"
spacing:
  xs: "4px"
  sm: "8px"
  md: "12px"
  lg: "16px"
  xl: "24px"
  xxl: "32px"
components:
  button-primary:
    backgroundColor: "{colors.primary}"
    textColor: "{colors.surface}"
    rounded: "{rounded.md}"
    padding: "10px 16px"
  button-primary-hover:
    backgroundColor: "{colors.primary-hover}"
    textColor: "{colors.surface}"
    rounded: "{rounded.md}"
    padding: "10px 16px"
  input-default:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.ink}"
    rounded: "{rounded.md}"
    padding: "9px 12px"
  card-default:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.ink}"
    rounded: "{rounded.lg}"
    padding: "16px"
---

# Design System: OmniConnect

## 1. Overview

**Creative North Star: "The Quiet Control Room"**

OmniConnect should feel like a calm SmartHome control room: status is visible, actions are close at hand, and technical depth stays available without interrupting everyday control. The interface is a product surface, not a marketing page; it earns trust through familiar controls, consistent density, clear labels, and restrained color.

The current WPF implementation is a functional baseline with default controls, DataGrids, and raw technical fields. P9 replaces that with a deliberate product UI while preserving Advanced views for telemetry logs, command details, automation executions, and debugging.

The system explicitly rejects the PRODUCT.md anti-references: raw admin panel, Swagger replacement, database browser, developer debug console, decorative gradient text, default glassmorphism, identical card grids, hero-metric templates, colored side-stripe cards, overly futuristic dashboards, and raw IDs in the normal flow.

**Key Characteristics:**

- Calm dashboard-first SmartHome control.
- Restrained accent color used for action, selection, and state.
- Standard product UI affordances before novelty.
- Technical complexity hidden behind Advanced mode.
- Clear status language for online/offline, telemetry, commands, automation, and notifications.

## 2. Colors

The palette is restrained: a clear blue primary, cool neutral surfaces, dark readable ink, and semantic state colors.

### Primary

- **Control Blue**: The primary action and current-selection color. Use it for main actions, selected navigation, enabled toggles, and meaningful highlights only.
- **Control Blue Hover**: The pressed/hover version of primary actions.
- **Soft Control Blue**: Low-emphasis selected backgrounds and information tints.

### Neutral

- **Quiet Background**: The app canvas. It should feel clean and product-like, never cream, parchment, or decorative.
- **Clear Surface**: Panels, cards, forms, and tables.
- **Muted Surface**: Sidebars, status bars, table headers, and low-emphasis containers.
- **Primary Ink**: Body and heading text.
- **Muted Ink**: Secondary labels and helper text.
- **Soft Border**: Dividers, input borders, and table boundaries.

### Tertiary

- **Success Green**: Completed commands, online states, valid confirmations.
- **Warning Amber**: Offline warnings, delayed command states, attention-needed notifications.
- **Danger Red**: Failed commands, validation errors, destructive states.
- **Info Cyan**: Informational notifications and neutral telemetry messages.

### Named Rules

**The Accent Earns Its Place Rule.** Blue appears only where the user can act, where something is selected, or where state needs emphasis. If a surface is blue just to look designed, it is wrong.

**The Status Has Meaning Rule.** Green, amber, red, and cyan are semantic state colors. Do not reuse them as decorative accents.

## 3. Typography

**Display Font:** Segoe UI, Inter, system-ui, sans-serif  
**Body Font:** Segoe UI, Inter, system-ui, sans-serif  
**Label/Mono Font:** Segoe UI, Inter, system-ui, sans-serif

**Character:** One familiar sans family keeps the app task-focused. The type should disappear into the workflow: readable labels, clear headings, and dense data without display-font drama.

### Hierarchy

- **Display** (700, 28px, 1.15): App title and major dashboard heading only.
- **Headline** (650, 22px, 1.2): Page-level titles such as Home, Devices, Automation.
- **Title** (600, 16px, 1.3): Card titles, panel headings, and form sections.
- **Body** (400, 14px, 1.45): Normal UI copy, table text, field values, and helper descriptions.
- **Label** (600, 12px, 1.25): Field labels, status labels, chips, compact metadata.

### Named Rules

**The Product Type Rule.** Do not use display fonts, decorative fonts, or oversized hero typography. This is a control app, not a landing page.

**The Label Stays Visible Rule.** Placeholders are not labels. Every input must have a visible label and a clear error message when invalid.

## 4. Elevation

OmniConnect uses tonal layering first and subtle shadows second. Surfaces separate through background, border, spacing, and hierarchy. Shadows appear only when they clarify interactive elevation, such as hover on a device card or a raised menu.

### Shadow Vocabulary

- **Surface Rest** (`0 1px 2px rgba(15, 23, 42, 0.06)`): Low card and panel separation.
- **Surface Hover** (`0 8px 24px rgba(15, 23, 42, 0.10)`): Interactive cards and menus on hover/open.
- **Focus Ring** (`0 0 0 3px rgba(37, 99, 235, 0.25)`): Keyboard focus and active form controls.

### Named Rules

**The Flat Until Touched Rule.** Surfaces are quiet at rest. Elevation increases only for hover, focus, menu, or dialog states.

## 5. Components

### Buttons

- **Shape:** Confident rounded rectangle (10px radius).
- **Primary:** Control Blue background, Clear Surface text, 10px 16px padding.
- **Hover / Focus:** Hover darkens to Control Blue Hover. Focus uses the blue focus ring and never removes keyboard visibility.
- **Secondary / Ghost:** Secondary actions use Clear Surface, Soft Border, Primary Ink. Ghost actions are text-like and reserved for low-risk secondary actions.

### Chips

- **Style:** Compact labels with Muted Surface background, Soft Border, and Primary Ink.
- **State:** Selected chips use Soft Control Blue and Control Blue text. State chips use semantic colors only for real status.

### Cards / Containers

- **Corner Style:** Rounded but not playful (16px radius).
- **Background:** Clear Surface on Quiet Background.
- **Shadow Strategy:** Surface Rest by default; Surface Hover only for interactive cards.
- **Border:** Soft Border for tables, panels, and cards where shape alone is not enough.
- **Internal Padding:** 16px minimum; 24px for major dashboard groups.

### Inputs / Fields

- **Style:** Clear Surface fill, Soft Border stroke, 10px radius, 9px 12px padding.
- **Focus:** Control Blue border plus Focus Ring.
- **Error / Disabled:** Danger Red border with message below the field; disabled controls use Muted Surface and Muted Ink.

### Navigation

- **Style:** Familiar product navigation. Current section uses Soft Control Blue background and Control Blue label/icon.
- **Typography:** 14px body weight for normal items; 600 weight for active item.
- **States:** Hover uses Muted Surface; focus uses Focus Ring; disabled uses Muted Ink.
- **Mobile / narrow treatment:** Collapse secondary details first. Do not shrink typography fluidly.

### SmartHome Control

- **Style:** Actuator controls should look like controls, not log entries. Boolean actuators use toggles or paired ON/OFF buttons. Sensor values use readable value cards with unit and timestamp.
- **Behavior:** Normal flow uses display names. Raw capability keys, command names, device IDs, and bitmasks belong in Advanced mode.

## 6. Do's and Don'ts

### Do:

- **Do** make Home the obvious starting point for device status, latest telemetry, actuator control, and notifications.
- **Do** use Control Blue only for main actions, current selection, enabled controls, and meaningful state.
- **Do** keep Advanced mode for raw telemetry, command logs, automation executions, and debugging.
- **Do** show empty states that teach the next action, such as claiming a device with a pairing code.
- **Do** design keyboard focus, loading, error, disabled, and success states for every interactive control.
- **Do** keep touch/click targets at least 44x44px where practical.
- **Do** use clear SmartHome language: online, offline, latest value, turn on, turn off, rule, notification.

### Don't:

- **Don't** make the primary experience look like a raw admin panel, Swagger replacement, database browser, or developer debug console.
- **Don't** use decorative gradient text.
- **Don't** use glassmorphism as default.
- **Don't** build identical card grids that do not reflect user tasks.
- **Don't** use hero-metric templates as decoration.
- **Don't** use colored side-stripe cards or alerts.
- **Don't** make the app overly futuristic or technical when the task is ordinary SmartHome control.
- **Don't** expose raw IDs, capability keys, command names, or bitmasks in the normal user flow.
- **Don't** use semantic state colors as decoration.
