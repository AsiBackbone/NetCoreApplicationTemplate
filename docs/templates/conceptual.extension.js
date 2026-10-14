// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Overrides the DocFX default no-op extension to add canonical and structured-data metadata.

var siteMetadata = require('./site-metadata.common.js')

/**
 * This method will be called at the start of exports.transform in conceptual.html.primary.js
 */
exports.preTransform = function (model) {
  return model
}

/**
 * This method will be called at the end of exports.transform in conceptual.html.primary.js
 */
exports.postTransform = function (model) {
  return siteMetadata.apply(
    model,
    siteMetadata.firstText(model.title),
    siteMetadata.firstText(model.description, model._description))
}
