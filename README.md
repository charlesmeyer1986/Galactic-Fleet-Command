# Galactic Fleet Command

## Overview

You are building a backend service for managing fleets in a fictional galactic command system. This system models fleets, processes commands asynchronously, and ensures that shared resources are allocated safely under concurrent conditions.

This exercise is designed to evaluate how you model systems, handle concurrency, design APIs, and make practical engineering tradeoffs.

## Time Expectation

This assignment is intended to take approximately 4-8 hours.

You are not expected to complete everything perfectly. Partial completion is acceptable as long as you document your decisions and tradeoffs.

## Tech Stack

Node.js and TypeScript plumbing have been included for you. If you prefer a different stack feel free to rebuild the base plumbing in your desired stack.  
Use in-memory storage only (no database required).  
You may use any libraries you feel are appropriate.

## Domain Model

Each fleet has a lifecycle:

Docked → Preparing → Ready → Deployed  
Preparing → FailedPreparation

Rules:

- Only fleets in Docked state can begin preparation
- A fleet must successfully reserve resources before becoming Ready
- If resource reservation fails, the fleet transitions to FailedPreparation
- Only Ready fleets can be deployed

A fleet should include at minimum:

- id
- name
- shipCount
- fuelRequired
- state

## Shared Resources

Assume a shared pool of resources (e.g., fuel).

Multiple fleets may attempt to reserve resources at the same time. Your system must ensure that resources are never over-allocated, even under concurrent requests.

## Commands

Commands represent actions performed on fleets and are processed asynchronously.

Required command:

PrepareFleetCommand

- Transitions fleet from Docked → Preparing
- Attempts to reserve resources
- On success → Ready
- On failure → FailedPreparation

Each command should have a status:

- Queued
- Processing
- Succeeded
- Failed

## Queue

Implement a simple in-memory queue:

- Commands are submitted via API
- A background worker processes commands asynchronously
- One worker is sufficient

Out of scope:

- Dead-letter queues
- Retry backoff strategies
- Scheduling or delayed execution
- Durable persistence

## API

Fleets:

POST /fleets — create a fleet  
GET /fleets/:id — retrieve a fleet  
PATCH /fleets/:id — update fleet properties

Commands:

POST /commands — submit a command  
GET /commands/:id — retrieve command status

System:

GET /health — health check

## Concurrency Requirement

Your system must correctly handle concurrent resource reservations.

Example: two commands attempt to reserve resources at the same time. Your system must ensure that resources are not double-allocated.

You may use any reasonable approach such as locks, mutexes, or other in-memory coordination strategies.

## Data Storage

Use in-memory data structures such as maps or arrays. No database is required.

## Testing

At minimum, include tests for:

- Valid and invalid fleet state transitions
- Resource reservation under concurrent conditions
- One end-to-end flow (API → command → state change)

## What We Care About

- Code clarity and structure
- Correctness of business logic
- Concurrency handling
- API design
- Thoughtful tradeoffs

## What We Do Not Expect

- Production-grade queue systems
- Advanced retry frameworks
- Full event sourcing implementations
- Complex infrastructure

## Bonus (Optional)

- Additional commands (e.g., DeployFleetCommand)
- Timeline/history of fleet transitions
- Logging or metrics
- Improved error handling

## Submission

Please include your source code and a short README describing:

- Design decisions
- Tradeoffs
- What you would improve with more time

