# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Development Commands

- **Development server**: `npm run dev` (runs on port 8080)
- **Build for production**: `npm run build`
- **Type checking**: `npm run type-check` 
- **Code formatting**: `npm run format`
- **Preview production build**: `npm run preview`

## Architecture Overview

This is a Vue 3 + Vuetify 3 + TypeScript application that serves as a Tag Metadata viewer/editor within a larger Mediator.Net dashboard system.

### Project Structure

- **Entry point**: `tagmetadata.html` → `src/view_tagmetadata/main.ts` → `ViewTagMetaData.vue`
- **Base path**: `/ViewBundle_TagMetaData/` (configured in vite.config.mts)
- **Build output**: `dist/` directory
- **Dev server port**: 8080

### Key Directories

- `src/view_tagmetadata/` - Main application components and logic
  - `ViewTagMetaData.vue` - Primary application component
  - `DlgConfigTag.vue` - Tag configuration dialog
  - `flowdiagram/` - Flow diagram editor components including FlowEditor.vue
  - `model_tags.ts` - Tag data models
  - `metamodel.ts` - Metadata model definitions
- `src/components/` - Reusable UI components (CodeEditor, TreeView, Splitter, etc.)
- `src/plugins/` - Vuetify and other plugin configurations
- `src/styles/` - Global styles and themes

### Technical Stack

- **Vue 3** with Composition API
- **Vuetify 3** for Material Design components
- **TypeScript** for type safety
- **Vite** for build tooling and development server
- **Dygraphs** for charting (type definitions included)
- **unplugin-vue-components** for automatic component imports

### Development Environment

The project includes debug utilities in `src/debug.ts` that set up the dashboard environment in development mode. Components are automatically imported via unplugin-vue-components configuration.

### Styling

Uses Vuetify's Material Design system with custom SCSS/Sass support via sass-embedded. Font loading is handled by vite-plugin-webfont-dl for Roboto fonts.

### Build Configuration

The Vite configuration includes:
- Asset copying from src/assets to build output
- Chunk size limit of 2000kb
- Vuetify optimization exclusions
- Alias `@` pointing to `src/` directory

### Related Projects

This is part of a larger Mediator.Net system with additional working directory at `D:\Fast\Mediator.Net\Mediator.Net`.