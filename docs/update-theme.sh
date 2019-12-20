git remote add -f darkfx-theme https://github.com/steffen-wilke/darkfx.git
git subtree add --prefix docs/templates/darkfx darkfx-theme master --squash

git fetch darkfx-theme master
git subtree pull --prefix docs/templates/darkfx darkfx-theme master --squash