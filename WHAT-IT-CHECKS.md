# What this gives you

Plain-language guide for the owner who can't read the code. This explains what a passing build actually proves — and the one thing it can't, where you're still the final eye.

---

## What does a green checkmark actually prove?

When the checks pass (the green checkmark on a build), these things have been confirmed for you, automatically, without anyone reading the code:

- **Every change goes through adversarial review before it lands.** AI reviewers — whose job is to find fault, not approve — check the change against what it was supposed to do, and a gate (the "commit gate") blocks any commit that doesn't have a recorded passing review bound to exactly that code. So unreviewed code can't slip in. *(One honest limit, so you know what the green light does and doesn't promise: today the gate proves a passing review was recorded for the code, not yet that the review couldn't be shortcut. Closing that — having the reviewers produce the verdict in code rather than by hand — is tracked in the repo's issues.)*

- **The core logic is verified against the right answers.** Your project's core logic is checked against the correct results worked out independently. Not "the code ran" — the actual values it produces are the values they should be.

- **The tool proved its own tests can catch broken work.** This is the part that lets you trust the green light instead of just hoping. The tool deliberately breaks copies of the code and confirms its own tests notice and fail. A test that passes no matter what is worthless; this proves those tests have teeth. (In the code this is the "mutation/tamper gate." It covers the core logic; extending it to other checks is tracked per project.)

- **Secrets, dependencies, and known-vulnerable code are scanned.** Three of GitHub's own free safety nets run on every change: one watches for passwords or keys accidentally left in the code (Secret Scanning), one watches for outside code your project relies on going out of date or becoming risky (Dependabot), and one scans for known security holes (CodeQL — this one runs on public repos; on a private repo the other two still run).

- **The code is auto-checked for cleanliness.** Two tools (ruff and bandit) automatically flag sloppy or unsafe code patterns before a change is committed — so what's built starts clean and stays clean.

These run on their own, the same way every time, on every build. You don't have to ask for them or stand over them.

---

## What is NOT machine-checked — where YOU are the final eye

A machine can prove the math is right. It cannot prove the result **looks** right, or that it's what you actually **meant**. That judgment is yours.

So every build produces a preview of the result and saves it for you to look at (the "visual-confirmation" preview — for the example add-on included, that's a render of its output). The checks above guarantee the logic is correct; this preview is for the one question they can't answer — *is this what I wanted?* That's your call, as the director.

Put simply: **green means the work is correct and built to standard. Whether it's the right work is still your eye.**
