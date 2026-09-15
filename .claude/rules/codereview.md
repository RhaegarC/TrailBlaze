# Code review

## Review against the deployment target

Verify a design against the environment it actually runs in — not the one the scaffold assumed,
and not the developer's laptop.

Ask, of anything that starts up, writes, or holds state:

- **What is it deployed to?** A `docker-compose.yml` was written and then removed: it existed
  because the inherited scaffold implied a local stack, while the real targets are Azure Container
  Apps for the API and Azure Static Web Apps for the web app. Neither consumes a compose file, so
  there was no stack to orchestrate.
- **How many of it runs?** A design correct on one instance can be wrong on many. Migrations
  applied by an `IHostedService` at startup were correct until ACA ran several replicas, which race
  each other over the same DDL. The same reasoning applies to feature 03's startup admin seeding.
- **What does it reach over the network?** Azure SQL Database is managed and has no local
  stand-in, so "it works on my machine" was never an available fallback — the connection string,
  the firewall, and the database's existence are all prerequisites rather than things the app
  arranges.

A design that has not been checked against its target is unverified, however green its tests are.
Tests here run with no database and a fake storage service, so they cannot speak to deployment
shape at all.

**How to apply:** when reviewing a change that runs on a host, say which host and how many
instances, and check the design against that. Do this *before* reviewing the code inside it — two
features of the foundation feature were rewritten because this question had not been asked.
