Contribution to .NEXT
====
[![Chat for Contributors](https://badges.gitter.im/dot_next/contrib.svg)](https://gitter.im/dot_next/contrib)

You can contribute to .NEXT or its documentation with Pull Requests and issues. Follow the rules described in this document.

## Branching Model
This repository uses branching model known as [git flow](https://nvie.com/posts/a-successful-git-branching-model/). Use **develop** as the destination branch in your Pull Request.

## Backward Compatibility
Contributions must not contain breaking changes such as backward incompatible modification of API signatures. The only exception is a new major version of the library. However, it should pass through code review and discussion.

## Unit Tests
If your PR contains bug fix or new feature then it should have unit tests.