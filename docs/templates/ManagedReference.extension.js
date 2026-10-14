// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Overrides the DocFX default no-op extension to add canonical and structured-data metadata
// to generated API reference pages.

var siteMetadata = require('./site-metadata.common.js')

function firstNameValue(name) {
  // DocFX models names as [{ lang, value }] lists; accept a plain string as well.
  if (Object.prototype.toString.call(name) === '[object Array]') {
    return name.length > 0 && name[0] ? siteMetadata.firstText(name[0].value) : null
  }

  return siteMetadata.firstText(name)
}

/**
 * This method will be called at the start of exports.transform in ManagedReference.html.primary.js
 */
exports.preTransform = function (model) {
  return model
}

/**
 * This method will be called at the end of exports.transform in ManagedReference.html.primary.js
 */
exports.postTransform = function (model) {
  var pageTitle = siteMetadata.firstText(
    model.title,
    firstNameValue(model.fullName),
    firstNameValue(model.name),
    model.uid)

  return siteMetadata.apply(model, pageTitle, null)
}
