// Canonical URL and schema.org structured data for AsiBackbone DocFX sites.
// Adapted from AsiBackbone Learning's conceptual.extension.js (the DocFX reference
// implementation in AsiBackbone/.github LANDING-PAGE-STANDARD.md). Values come from
// docfx.json globalMetadata so each repository owns its own names and URLs.
// DocFX runs template scripts in Jint, so this file stays ES5.

function firstText() {
  for (var index = 0; index < arguments.length; index++) {
    var value = arguments[index]

    if (value !== undefined && value !== null && typeof value !== 'object' && String(value).trim() !== '') {
      return String(value).trim()
    }
  }

  return null
}

function normalizeCanonicalBaseUrl(value) {
  var baseUrl = firstText(value)

  if (!baseUrl) {
    return null
  }

  return baseUrl.replace(/\/+$/, '') + '/'
}

function buildCanonicalUrl(baseUrl, outputPath) {
  var siteRoot = normalizeCanonicalBaseUrl(baseUrl)
  var path = firstText(outputPath)

  if (!siteRoot || !path) {
    return null
  }

  path = path
    .replace(/\\/g, '/')
    .replace(/^\.\//, '')
    .replace(/^\/+/, '')

  // GitHub Pages serves index.html through both explicit and directory-style URLs.
  // Prefer the directory form for indexes while leaving ordinary .html paths intact.
  if (path.toLowerCase() === 'index.html') {
    return siteRoot
  }

  if (/\/index\.html$/i.test(path)) {
    return siteRoot + path.slice(0, -'index.html'.length)
  }

  return siteRoot + path
}

function safeJson(value) {
  return JSON.stringify(value)
    .replace(/</g, '\\u003c')
    .replace(/>/g, '\\u003e')
    .replace(/&/g, '\\u0026')
    .replace(/\u2028/g, '\\u2028')
    .replace(/\u2029/g, '\\u2029')
}

function buildStructuredData(model, pageTitle, pageDescription) {
  var siteRoot = normalizeCanonicalBaseUrl(model._canonicalBaseUrl)
  var canonicalUrl = firstText(model._canonicalUrl)

  if (!siteRoot || !canonicalUrl) {
    return null
  }

  var siteName = firstText(model._structuredDataSiteName, model._appName)
  var siteDescription = firstText(model._structuredDataSiteDescription)
  var publisherName = firstText(model._structuredDataPublisherName)
  var publisherUrl = firstText(model._structuredDataPublisherUrl)
  var websiteId = siteRoot + '#website'
  var publisherId = siteRoot + '#publisher'
  var graph = []

  var website = {
    '@type': 'WebSite',
    '@id': websiteId,
    url: siteRoot
  }

  if (siteName) {
    website.name = siteName
  }

  if (siteDescription) {
    website.description = siteDescription
  }

  if (firstText(model._lang)) {
    website.inLanguage = firstText(model._lang)
  }

  if (publisherName) {
    website.publisher = { '@id': publisherId }
  }

  graph.push(website)

  if (publisherName) {
    var publisher = {
      '@type': 'Organization',
      '@id': publisherId,
      name: publisherName
    }

    if (publisherUrl) {
      publisher.url = publisherUrl
    }

    graph.push(publisher)
  }

  var webPage = {
    '@type': 'WebPage',
    '@id': canonicalUrl + '#webpage',
    url: canonicalUrl,
    name: firstText(pageTitle, siteName),
    isPartOf: { '@id': websiteId }
  }

  if (pageDescription) {
    webPage.description = pageDescription
  }

  if (firstText(model._socialImageUrl)) {
    webPage.primaryImageOfPage = {
      '@type': 'ImageObject',
      url: firstText(model._socialImageUrl)
    }
  }

  graph.push(webPage)

  return safeJson({
    '@context': 'https://schema.org',
    '@graph': graph
  })
}

// Applies canonical and structured-data metadata to a page model.
// pageTitle and pageDescription are plain text chosen by the calling document type.
exports.apply = function (model, pageTitle, pageDescription) {
  model._canonicalUrl = buildCanonicalUrl(model._canonicalBaseUrl, model._path)
  model._structuredDataJson = buildStructuredData(model, pageTitle, pageDescription)
  return model
}

exports.firstText = firstText
exports.buildCanonicalUrl = buildCanonicalUrl
exports.safeJson = safeJson
