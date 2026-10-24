# Native fonts

Retrieved 24 October 2026 from official Google Fonts:

- https://raw.githubusercontent.com/google/fonts/main/ofl/outfit/Outfit%5Bwght%5D.ttf
- https://raw.githubusercontent.com/google/fonts/main/ofl/dmsans/DMSans%5Bopsz%2Cwght%5D.ttf

Corresponding OFL.txt files are retained as Outfit-OFL.txt and DMSans-OFL.txt. Both SIL OFL 1.1.

Source SHA-256:

```text
Outfit.ttf FC7287273E66929776E2BA54F144FE699080BEC29F61BF649D70D871468AEADE
DMSans.ttf 8CD08D97E89C24D0AA92EDD2F0F4C8EE6195EEE9B7C9F154865A58B02F0C1C0D
```

Source variable fonts are excluded from runtime resources. Derived Regular/400 and SemiBold/600 faces are embedded for predictable native weight selection; DM Sans optical size is 14. Reproduce using Python with fonttools 4.60.1: run `python tools/instantiate-fonts.py` from the companion folder. This conversion tool is not an app dependency. Retain licenses and update hashes when replacing sources. Sources are hash-identified, not upstream-commit-pinned.
