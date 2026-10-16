# Fire burn checks

Allow Unity to import scripts. No new scene setup is required for an existing Fire
prototype. Select Assets/Definitions/Weapons/FireSpellPrototype.asset and edit
Burn Percent and Burn Duration. New defaults: 20 percent, 3 seconds. Existing setup
commands preserve tuning. Values are shared configuration; burn state is per target.

Percent is additional base attack damage per second: a 35 damage fireball at 20%
adds 7 DPS for 3 seconds (21 additional damage). Flame at 30 base DPS adds 6 burn DPS.
Repeated hits refresh one duration; they do not stack. The stronger active rate wins.
Set either value to zero to prevent new burns; existing burns finish their snapshot.

Unity gameplay checks still required:
- Hit a healthy bot once, stop firing, and inspect CurrentHealth: direct damage first,
  then gradual burn loss for the configured duration. A burn can kill the bot.
- Change percentage/duration; verify damage totals. Test zero for either setting.
- Briefly flame a bot, step away, and verify lingering damage then expiry.
- Repeat hits, including child colliders: one burn rate, refreshed duration.
- Walls/misses must not apply burn. Bot projectiles must retain direct damage only.
- Re-enable a defeated test target: no old burn. Die/respawn while burning: full
  health with no lingering burn. Launched fireballs/burns survive caster death.
- Recheck shared ammo, overheat and exclusive MB1/MB2 input.

Standalone Runtime/Editor compilation is checked by the agent; Unity timing,
physics and gameplay are not exercised by standalone compilation.

Deferred, lower priority: outward burn indicators on affected actors and a burn status indicator on the player's health bar. Neither is implemented yet.
