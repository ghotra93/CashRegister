# 5. Self-critique

## What I would change, and why

- The divisor selected in the UI is kept in memory, which made v1 simple but is not the right way to do it in a production system. In an AWS shop I would store it in AWS Parameter Store and load the value dynamically at pod start-up.
- For this exercise I chose a spec-driven development approach, which may be more than the exercise intends. I chose it to show how I would incorporate AI into my development process.

## Strongest part, and why

- Because I went with spec-driven development and a build harness, every change is backed by test cases; these are listed in [03-verification.md](03-verification.md).
- Engine extensibility is the strongest part. I chose rule classes with configurable priorities to make the design future-proof: if two rules conflict later, the priority decides which one takes precedence. That lets us add or change rules without significantly changing the core engine.

## Weakest part, and why

The weakest part is that state is managed in memory. On a pod restart the divisor resets to 3. It also limits us to a single pod, since multiple pods would each hold their own divisor value. For the sake of simplicity I chose this approach.
