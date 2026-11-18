# Card Throwing Unity Demo

This is a small Unity test project for the zombie game's 54-card weapon.

## Open the project

1. Extract the ZIP.
2. Open Unity Hub.
3. Select **Add project from disk**.
4. Choose the `CardThrowingDemo` folder.
5. Open it with Unity 2022.3 LTS or a newer compatible Unity version.
6. Unity automatically creates and opens `Assets/Scenes/CardThrowingDemo.unity` after the scripts compile.
7. Press **Play**.

If Unity opens a blank scene, open `Assets/Scenes/CardThrowingDemo.unity` manually.

## Controls

- **WASD:** Move
- **Mouse:** Look
- **Left Click:** Draw and immediately throw the next card
- **1:** Add +5 additive damage
- **2:** Add +0.5 to the damage multiplier
- **0:** Reset damage modifiers
- **R:** Mark the deck empty so the next throw tests the 54-card reshuffle
- **Escape:** Unlock or lock the mouse cursor

## Included mechanics

- A locked 54-card deck containing 52 standard cards and 2 Jokers
- Cards are shuffled before use
- Drawn cards must be thrown and cannot be stored
- Cards 2 through 10 deal their rank as damage
- Jack deals 11, Queen 12, King 13, and Ace 14 damage
- Jokers instantly defeat regular zombies
- Jokers remove 50% of a boss's remaining health
- Additive and multiplicative damage modifiers
- The deck automatically rebuilds and reshuffles after all 54 cards are used
- Debug display for the next card, cards remaining, and damage modifiers

## Test targets

- Walker: 30 health
- Sprinter: 45 health
- Thrower: 60 health
- Boss: 250 health

The targets are stationary so the card damage and Joker behavior are easy to test.

## Main scripts

- `ThrowingCard.cs`: Deck creation, shuffling, drawing, throwing, and damage modifiers
- `CardProjectile.cs`: Projectile movement, collision, and Joker behavior
- `ZombieHealth.cs`: Example damageable zombie and boss health class
- `DemoSceneSetup.cs`: Creates the player, arena, targets, camera, and controls
