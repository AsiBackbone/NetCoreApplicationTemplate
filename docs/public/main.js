// The DocFX modern template has no skip link and its <main> element has no id.
// Add both at startup so keyboard users can bypass the navbar on every page.

export default {
  start: function () {
    var main = document.querySelector('main')

    if (!main || document.querySelector('.skip-link')) {
      return
    }

    if (!main.id) {
      main.id = 'main-content'
    }

    main.setAttribute('tabindex', '-1')

    var skipLink = document.createElement('a')
    skipLink.className = 'skip-link'
    skipLink.href = '#' + main.id
    skipLink.textContent = 'Skip to content'
    document.body.insertBefore(skipLink, document.body.firstChild)
  }
}
