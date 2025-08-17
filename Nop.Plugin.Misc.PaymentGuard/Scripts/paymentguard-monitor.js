// Enhanced PaymentGuard Monitor - Preserving all existing functionality + AJAX monitoring
(function (window, document) {
  'use strict';

  var PaymentGuard = {
    // EXISTING configuration
    config: {
      apiEndpoint: window.PaymentGuardConfig?.apiEndpoint || '/Plugins/PaymentGuard/Api',
      checkInterval: 5000,
      enabled: window.PaymentGuardConfig?.enabled || true,
      enableSRI: window.PaymentGuardConfig?.enableSRI || true
    },

    // EXISTING properties
    initialScripts: [],
    currentScripts: [],

    // NEW properties for enhanced AJAX monitoring
    detectedScripts: new Map(),
    paymentScripts: new Set(),
    sessionId: generateSessionId(),
    lastAjaxCheck: Date.now(),
    isEnhancedMode: true,

    // EXISTING init method - enhanced
    init: function () {
      if (!this.config.enabled) return;

      console.log('PaymentGuard: Enhanced monitoring initialized');
      
      this.captureInitialScripts();
      this.startMonitoring();
      this.setupCSPViolationReporting();
      
      // NEW: Enhanced AJAX monitoring
      this.setupAjaxInterception();
      this.setupPaymentMethodMonitoring();
      this.reportSessionStart();
    },

    // EXISTING captureInitialScripts - enhanced with SRI information
    captureInitialScripts: function () {
      var scripts = document.querySelectorAll('script[src]');
      this.initialScripts = Array.from(scripts).map(function (script) {
        return {
          src: script.src,
          integrity: script.integrity || null,
          crossorigin: script.crossOrigin || null,
          async: script.async,
          defer: script.defer,
          type: script.type || 'text/javascript',
          hasSRI: !!script.integrity,
          sriAlgorithm: script.integrity ? script.integrity.split('-')[0] : null
        };
      });

      // Enhanced: Validate SRI for initial scripts
      this.validateInitialScriptsSRI();
      console.log('PaymentGuard: Captured', this.initialScripts.length, 'initial scripts');
    },

    // NEW: Validate SRI integrity for initial scripts
    validateInitialScriptsSRI: function () {
      if (!this.config.enableSRI) return;

      var self = this;
      this.initialScripts.forEach(function (scriptInfo) {
        //console.log(scriptInfo);
        if (scriptInfo.hasSRI) {
          self.validateScriptSRI(scriptInfo);
        } else if (self.shouldHaveSRI(scriptInfo.src)) {
          // BLOCK the script execution
          self.blockUnsafeScript(scriptInfo.src);
          self.reportSRIViolation(scriptInfo.src, 'missing-sri');
        }
      });
    },

    blockUnsafeScript: function (scriptUrl) {
      console.error('PaymentGuard: BLOCKING unsafe script:', scriptUrl);

      // Find and disable the script element
      var scripts = document.querySelectorAll('script[src="' + scriptUrl + '"]');
      scripts.forEach(function (script) {
        if (script.parentNode) {
          script.parentNode.removeChild(script);
          console.warn('PaymentGuard: Removed unsafe script element:', scriptUrl);
        }
      });

      var scriptDomain = this.getScriptDomain(scriptUrl);
      // Remove iframes from the same domain
      var iframes = document.querySelectorAll('iframe[src*="' + scriptDomain + '"]');
      iframes.forEach(function (iframe) {
        if (iframe.parentNode) {
          iframe.parentNode.removeChild(iframe);
          console.warn('PaymentGuard: Removed iframe from blocked domain:', iframe.src);
        }
      });

      // Show simple alert for payment scripts
      if (this.isPaymentScript(scriptUrl)) {
        this.showPaymentBlockedWarning(scriptUrl);
      }

      // Report to monitoring service
      this.reportBlockedScript(scriptUrl);
    },

    showPaymentBlockedWarning: function (scriptUrl) {
      var message = 'SECURITY ALERT:\n\n' +
        'Payment processing has been temporarily blocked for your security.\n\n' +
        'Reason: Unverified payment script detected\n' +
        'Script: ' + this.getScriptDomain(scriptUrl) + '\n\n' +
        'Please contact support or try refreshing the page.\n\n' +
        'Your security is our priority.';

      alert(message);
    },

    reportBlockedScript: function (scriptUrl) {
      var data = {
        scriptUrl: scriptUrl,
        pageUrl: window.location.href,
        timestamp: new Date().toISOString(),
        userAgent: navigator.userAgent,
        blockReason: 'missing-sri-validation'
      };

      // Send to your monitoring endpoint
      fetch(this.config.apiEndpoint + '/ReportBlockedScript', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(data)
      }).catch(function (err) {
        console.error('PaymentGuard: Failed to report blocked script:', err);
      });
    },

    // Helper to get clean domain name for user display
    getScriptDomain: function (scriptUrl) {
      try {
        var url = new URL(scriptUrl);
        return url.hostname;
      } catch (e) {
        return scriptUrl.substring(0, 50) + '...';
      }
    },

    // Enhanced: shouldHaveSRI - using consistent local script detection
    shouldHaveSRI: function (scriptUrl) {
      // Skip local scripts using the same logic as isLocalScript
      if (this.isLocalScript(scriptUrl)) {
        return false;
      }

      // Check if it's a trusted CDN that should have SRI
      var trustedCDNs = window.PaymentGuardConfig.trustedDomains;

      // Check if it's a payment script (these should be monitored closely)
      var isPaymentScript = this.isPaymentScript(scriptUrl);
      var isTrustedCDN = trustedCDNs.some(function (cdn) {
        return scriptUrl.includes(cdn);
      });

      // Require SRI for trusted CDNs OR payment scripts
      return isTrustedCDN || isPaymentScript;
    },

    // EXISTING validateScriptSRI - preserved
    validateScriptSRI: function (scriptInfo) {
      if (!this.config.enableSRI) return;

      fetch(this.config.apiEndpoint + '/ValidateScriptWithSRI', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          scriptUrl: scriptInfo.src,
          integrity: scriptInfo.integrity,
          pageUrl: window.location.href
        })
      })
        .then(function (response) {
          return response.json();
        })
        .then(function (data) {
          if (data.success) {
            if (!data.hasValidSRI) {
              console.error('PaymentGuard: SRI validation failed for script:', scriptInfo.src);
              console.error('PaymentGuard: SRI Error:', data.sriError);
              PaymentGuard.reportSRIViolation(scriptInfo.src, 'sri-validation-failed', data.sriError);
            } else {
              console.log('PaymentGuard: SRI validation passed for script:', scriptInfo.src);
            }

            if (!data.isAuthorized) {
              console.warn('PaymentGuard: Unauthorized script detected:', scriptInfo.src);
              PaymentGuard.handleUnauthorizedScript(scriptInfo);
            }
          } else {
            console.error('PaymentGuard: Error validating script with SRI:', data.error);
          }
        })
        .catch(function (error) {
          console.error('PaymentGuard: Error validating SRI for script:', scriptInfo.src, error);
        });
    },

    // EXISTING reportSRIViolation - preserved
    reportSRIViolation: function (scriptUrl, violationType, details) {
      if (!this.config.enableSRI) return;

      var violation = {
        src: scriptUrl,
        violation: violationType,
        details: details,
        timestamp: new Date().toISOString(),
        userAgent: navigator.userAgent
      };

      console.warn('PaymentGuard: SRI Violation -', violationType, 'for script:', scriptUrl);

      fetch(this.config.apiEndpoint + '/ReportViolation', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          violationType: violationType,
          scriptUrl: scriptUrl,
          pageUrl: window.location.href,
          timestamp: violation.timestamp,
          userAgent: violation.userAgent,
          details: details
        })
      })
        .catch(function (error) {
          console.error('PaymentGuard: Error reporting SRI violation:', error);
        });
    },

    // EXISTING handleNewScript - enhanced for AJAX scenarios
    handleNewScript: function (scriptElement, context) {
      var scriptInfo = {
        src: scriptElement.src || 'inline-script-' + this.generateSimpleHash(scriptElement.textContent || ''),
        integrity: scriptElement.integrity || null,
        addedAt: new Date().toISOString(),
        authorized: false,
        hasSRI: !!scriptElement.integrity,
        context: context || 'unknown'
      };

      console.warn('PaymentGuard: New script detected:', scriptInfo.src, 'Context:', scriptInfo.context);

      // Track script
      this.detectedScripts.set(scriptInfo.src, scriptInfo);

      // Check if it's a payment script
      if (this.isPaymentScript(scriptInfo.src)) {
        this.paymentScripts.add(scriptInfo.src);
        console.log('PaymentGuard: Payment script detected:', scriptInfo.src);
        this.reportPaymentScript(scriptInfo);
      }

      // Enhanced SRI validation logic:
      // 1. If script HAS integrity attribute, validate it
      if (scriptInfo.hasSRI) {
        this.validateScriptSRI(scriptInfo);
      }
      // 2. If script SHOULD HAVE SRI but doesn't, report missing SRI
      else if (this.shouldHaveSRI(scriptInfo.src)) {
        this.reportSRIViolation(scriptInfo.src, 'missing-sri');
        // Still validate for authorization even without SRI
        this.validateScript(scriptInfo);
      }
      // 3. For all other scripts, just validate authorization
      else {
        this.validateScript(scriptInfo);
      }

      // Special handling for payment scripts without SRI
      if (this.isPaymentScript(scriptInfo.src) && !scriptInfo.hasSRI) {
        console.warn('PaymentGuard: Payment script without SRI detected:', scriptInfo.src);
        // You might want to still call ValidateScriptWithSRI to generate a hash
        this.validateScriptWithSRIForced(scriptInfo);
      }
    },

    // NEW: Force SRI validation even for scripts without integrity attribute
    validateScriptWithSRIForced: function (scriptInfo) {
      if (!this.config.enableSRI) return;

      fetch(this.config.apiEndpoint + '/ValidateScriptWithSRI', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          scriptUrl: scriptInfo.src,
          integrity: null, // No integrity provided
          pageUrl: window.location.href,
          forceValidation: true, // Flag to indicate forced validation
          context: scriptInfo.context,
          sessionId: this.sessionId,
          timestamp: new Date().toISOString()
        })
      })
      .then(function(response) {
        return response.json();
      })
      .then(function(data) {
        if (data.success) {
          console.log('PaymentGuard: Forced SRI validation for payment script:', scriptInfo.src);
          console.log('PaymentGuard: Generated hash:', data.generatedHash);
          
          if (!data.isAuthorized) {
            console.warn('PaymentGuard: Unauthorized payment script detected:', scriptInfo.src);
            PaymentGuard.handleUnauthorizedScript(scriptInfo);
          }
        } else {
          console.error('PaymentGuard: Error in forced SRI validation:', data.error);
        }
      })
      .catch(function(error) {
        console.error('PaymentGuard: Error in forced SRI validation for script:', scriptInfo.src, error);
      });
    },

    // EXISTING startMonitoring - enhanced with AJAX support
    startMonitoring: function () {
      // Enhanced MutationObserver to catch SRI attributes
      if (window.MutationObserver) {
        var observer = new MutationObserver(function (mutations) {
          mutations.forEach(function (mutation) {
            if (mutation.type === 'childList') {
              mutation.addedNodes.forEach(function (node) {
                if (node.tagName === 'SCRIPT') {
                  PaymentGuard.handleNewScript(node, 'dom-mutation');
                }
              });
            }
            // Watch for attribute changes (like integrity being added/removed)
            else if (mutation.type === 'attributes' &&
              mutation.target.tagName === 'SCRIPT' &&
              mutation.attributeName === 'integrity') {
              console.log('PaymentGuard: Script integrity attribute changed:', mutation.target.src);
              PaymentGuard.handleNewScript(mutation.target, 'attribute-change');
            }
          });
        });

        observer.observe(document.head, {
          childList: true,
          subtree: true,
          attributes: true,
          attributeFilter: ['integrity', 'src']
        });
        observer.observe(document.body, {
          childList: true,
          subtree: true,
          attributes: true,
          attributeFilter: ['integrity', 'src']
        });
      }

      // Periodic check
      setInterval(function () {
        PaymentGuard.performPeriodicCheck();
      }, this.config.checkInterval);
    },

    // EXISTING performPeriodicCheck - preserved
    performPeriodicCheck: function () {
      this.currentScripts = Array.from(document.querySelectorAll('script[src]')).map(function (script) {
        return {
          src: script.src,
          integrity: script.integrity,
          hasSRI: !!script.integrity
        };
      });

      // Check for new scripts
      var newScripts = this.currentScripts.filter(function (current) {
        return !PaymentGuard.initialScripts.some(function (initial) {
          return initial.src === current.src;
        });
      });

      if (newScripts.length > 0) {
        console.warn('PaymentGuard: New scripts detected during periodic check:', newScripts);
        newScripts.forEach(function (scriptInfo) {
          if (scriptInfo.hasSRI) {
            PaymentGuard.validateScriptSRI(scriptInfo);
          } else if (PaymentGuard.shouldHaveSRI(scriptInfo.src)) {
            PaymentGuard.reportSRIViolation(scriptInfo.src, 'missing-sri');
          }
        });
        this.reportNewScripts(newScripts);
      }
    },

    // EXISTING validateScript - preserved
    validateScript: function (scriptInfo) {
      fetch(this.config.apiEndpoint + '/ValidateScript', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          scriptUrl: scriptInfo.src,
          pageUrl: window.location.href,
          timestamp: new Date().toISOString()
        })
      })
        .then(function (response) {
          return response.json();
        })
        .then(function (data) {
          if (!data.isAuthorized) {
            console.error('PaymentGuard: Unauthorized script detected:', scriptInfo.src);
            PaymentGuard.handleUnauthorizedScript(scriptInfo);
          }
        })
        .catch(function (error) {
          console.error('PaymentGuard: Error validating script:', error);
        });
    },

    // EXISTING reportNewScripts - preserved
    reportNewScripts: function (scripts) {
      fetch(this.config.apiEndpoint + '/ReportScripts', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          scripts: scripts.map(function(s) { return s.src; }),
          pageUrl: window.location.href,
          userAgent: navigator.userAgent,
          timestamp: new Date().toISOString(),
          initialScripts: this.initialScripts.map(function (s) { return s.src; })
        })
      })
        .catch(function (error) {
          console.error('PaymentGuard: Error reporting scripts:', error);
        });
    },

    // EXISTING handleUnauthorizedScript - preserved
    handleUnauthorizedScript: function (scriptInfo) {
      console.error('PaymentGuard: SECURITY ALERT - Unauthorized script:', scriptInfo);
      this.reportSecurityViolation(scriptInfo);
    },

    // EXISTING reportSecurityViolation - preserved
    reportSecurityViolation: function (scriptInfo) {
      fetch(this.config.apiEndpoint + '/ReportViolation', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          violationType: 'unauthorized-script',
          scriptUrl: scriptInfo.src,
          pageUrl: window.location.href,
          timestamp: new Date().toISOString(),
          userAgent: navigator.userAgent
        })
      })
        .catch(function (error) {
          console.error('PaymentGuard: Error reporting violation:', error);
        });
    },

    // EXISTING setupCSPViolationReporting - preserved
    setupCSPViolationReporting: function () {
      document.addEventListener('securitypolicyviolation', function (event) {
        console.warn('PaymentGuard: CSP Violation detected:', event);

        PaymentGuard.reportCSPViolation({
          blockedURI: event.blockedURI,
          violatedDirective: event.violatedDirective,
          originalPolicy: event.originalPolicy,
          effectiveDirective: event.effectiveDirective,
          sourceFile: event.sourceFile,
          lineNumber: event.lineNumber,
          columnNumber: event.columnNumber
        });
      });
    },

    // EXISTING reportCSPViolation - preserved
    reportCSPViolation: function (violationData) {
      fetch(this.config.apiEndpoint + '/ReportCSPViolation', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          violation: violationData,
          pageUrl: window.location.href,
          timestamp: new Date().toISOString(),
          userAgent: navigator.userAgent
        })
      })
        .catch(function (error) {
          console.error('PaymentGuard: Error reporting CSP violation:', error);
        });
    },

    // EXISTING generateSimpleHash - preserved
    generateSimpleHash: function (str) {
      var hash = 0;
      if (str.length === 0) return hash.toString();
      for (var i = 0; i < str.length; i++) {
        var char = str.charCodeAt(i);
        hash = ((hash << 5) - hash) + char;
        hash = hash & hash;
      }
      return Math.abs(hash).toString(16);
    },

    // NEW: AJAX interception for single page checkout
    setupAjaxInterception: function() {
      var self = this;
      
      // Override XMLHttpRequest
      var originalXHR = window.XMLHttpRequest;
      window.XMLHttpRequest = function() {
        var xhr = new originalXHR();
        var originalSend = xhr.send;
        
        xhr.send = function() {
          var preScripts = self.captureCurrentScriptSignature();
          
          xhr.addEventListener('load', function() {
            setTimeout(function() {
              self.handleAjaxResponse(preScripts, 'xhr');
            }, 200);
          });
          
          return originalSend.apply(this, arguments);
        };
        
        return xhr;
      };

      // Override fetch
      var originalFetch = window.fetch;
      window.fetch = function() {
        var preScripts = self.captureCurrentScriptSignature();
        
        return originalFetch.apply(this, arguments).then(function(response) {
          setTimeout(function() {
            self.handleAjaxResponse(preScripts, 'fetch');
          }, 200);
          return response;
        });
      };

      // Override jQuery AJAX if available
      if (window.jQuery && jQuery.ajax) {
        var originalAjax = jQuery.ajax;
        jQuery.ajax = function(settings) {
          var preScripts = self.captureCurrentScriptSignature();
          
          var originalSuccess = settings.success;
          settings.success = function() {
            if (originalSuccess) originalSuccess.apply(this, arguments);
            setTimeout(function() {
              self.handleAjaxResponse(preScripts, 'jquery');
            }, 200);
          };
          
          return originalAjax.call(this, settings);
        };
      }
    },

    // NEW: Payment method specific monitoring
    setupPaymentMethodMonitoring: function() {
      var self = this;
      
      // Monitor payment method selection
      document.addEventListener('change', function(e) {
        if (self.isPaymentMethodField(e.target)) {
          console.log('PaymentGuard: Payment method changed:', e.target.value);
          setTimeout(function() {
            self.scanForPaymentScripts(e.target.value);
          }, 500);
        }
      });

      // Monitor payment field interactions
      document.addEventListener('focus', function(e) {
        if (self.isPaymentField(e.target)) {
          setTimeout(function() {
            self.scanForPaymentScripts('payment-focus');
          }, 100);
        }
      });
    },

    // NEW: Handle AJAX responses and detect new scripts with logging
    handleAjaxResponse: function(preScripts, source) {
      var currentScripts = this.captureCurrentScriptSignature();
      
      if (currentScripts !== preScripts) {
        console.log('PaymentGuard: Scripts changed after', source);
        
        var currentScriptList = this.captureCurrentScriptList();
        var preScriptList = preScripts.split('|').filter(function(s) { return s.length > 0; });
        var newScripts = currentScriptList.filter(function(script) {
          return !preScriptList.includes(script);
        });

        if (newScripts.length > 0) {
          console.log('PaymentGuard: New scripts detected via AJAX:', newScripts);
          
          // Log to ScriptMonitoringLog table
          this.reportAjaxMonitoring(newScripts, preScriptList, source);
          
          // Process new scripts
          var self = this;
          newScripts.forEach(function(scriptSrc) {
            var scriptElement = document.querySelector('script[src="' + scriptSrc + '"]');
            if (scriptElement) {
              self.handleNewScript(scriptElement, 'ajax-' + source);
            }
          });
        }
      }
      
      this.lastAjaxCheck = Date.now();
    },

    // NEW: Report AJAX monitoring to ScriptMonitoringLog
    reportAjaxMonitoring: function(newScripts, preAjaxScripts, ajaxSource) {
      fetch(this.config.apiEndpoint + '/ReportAjaxMonitoring', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          sessionId: this.sessionId,
          pageUrl: window.location.href,
          newScripts: newScripts,
          preAjaxScripts: preAjaxScripts,
          ajaxSource: ajaxSource,
          context: 'ajax-script-detection',
          userAgent: navigator.userAgent,
          timestamp: new Date().toISOString()
        })
      })
      .then(function(response) {
        return response.json();
      })
      .then(function(data) {
        if (data.success) {
          console.log('PaymentGuard: AJAX monitoring logged - Log ID:', data.logId, 'New scripts:', data.newScriptsCount);
          if (data.unauthorizedCount > 0) {
            console.warn('PaymentGuard: AJAX detected', data.unauthorizedCount, 'unauthorized scripts');
          }
        }
      })
      .catch(console.error);
    },

    // NEW: Payment script detection and reporting with logging
    scanForPaymentScripts: function(context) {
      var allScripts = document.querySelectorAll('script[src]');
      var self = this;
      var newPaymentScripts = [];
      
      Array.from(allScripts).forEach(function(script) {
        if (self.isPaymentScript(script.src) && !self.paymentScripts.has(script.src)) {
          console.log('PaymentGuard: Payment script found:', script.src, 'Context:', context);
          self.paymentScripts.add(script.src);
          newPaymentScripts.push(script.src);
          self.handleNewScript(script, 'payment-' + context);
        }
      });

      // Log payment method monitoring if new payment scripts found
      if (newPaymentScripts.length > 0) {
        this.reportPaymentMethodMonitoring(newPaymentScripts, context);
      }
    },

    // NEW: Report payment method monitoring to ScriptMonitoringLog
    reportPaymentMethodMonitoring: function(paymentScripts, context) {
      // Extract payment method from context or try to detect it
      var paymentMethod = this.detectCurrentPaymentMethod() || context;

      fetch(this.config.apiEndpoint + '/ReportPaymentMethodMonitoring', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          sessionId: this.sessionId,
          pageUrl: window.location.href,
          paymentMethod: paymentMethod,
          paymentScripts: paymentScripts,
          context: context,
          userAgent: navigator.userAgent,
          timestamp: new Date().toISOString()
        })
      })
      .then(function(response) {
        return response.json();
      })
      .then(function(data) {
        if (data.success) {
          console.log('PaymentGuard: Payment method monitoring logged - Log ID:', data.logId, 'Method:', data.paymentMethod, 'Security:', data.securityLevel);
          if (data.unauthorizedCount > 0) {
            console.error('PaymentGuard: CRITICAL - Unauthorized payment scripts detected for', data.paymentMethod);
          }
        }
      })
      .catch(console.error);
    },

    // NEW: Try to detect current payment method
    detectCurrentPaymentMethod: function() {
      // Try to find selected payment method
      var paymentRadios = document.querySelectorAll('input[type="radio"][name*="payment"]:checked');
      if (paymentRadios.length > 0) {
        return paymentRadios[0].value || 'unknown';
      }

      // Try to detect from script URLs
      var scripts = Array.from(this.paymentScripts);
      var paymentProviders = window.PaymentGuardConfig.paymentProviders;

      for (var script of scripts) {
        for (var provider of paymentProviders) {
          if (script.toLowerCase().includes(provider.toLowerCase())) {
            return provider;
          }
        }
      }

      return 'unknown';
    },

    reportPaymentScript: function(scriptInfo) {
      fetch(this.config.apiEndpoint + '/ReportViolation', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          violationType: 'payment-script-detected',
          scriptUrl: scriptInfo.src,
          pageUrl: window.location.href,
          context: scriptInfo.context,
          sessionId: this.sessionId,
          timestamp: new Date().toISOString(),
          userAgent: navigator.userAgent
        })
      }).catch(console.error);
    },

    reportSessionStart: function() {
      var allScripts = this.captureCurrentScriptList();
      var externalScripts = allScripts.filter(function(src) {
        return !PaymentGuard.isLocalScript(src);
      });
      var paymentScripts = externalScripts.filter(function(src) {
        return PaymentGuard.isPaymentScript(src);
      });

      fetch(this.config.apiEndpoint + '/ReportMonitoringSession', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          sessionId: this.sessionId,
          pageUrl: window.location.href,
          detectedScripts: allScripts, // Send all for complete logging
          paymentScripts: paymentScripts,
          headers: this.captureSecurityHeaders(),
          userAgent: navigator.userAgent,
          context: 'session-start',
          checkType: 'enhanced-client-monitoring',
          timestamp: new Date().toISOString()
        })
      })
      .then(function(response) {
        return response.json();
      })
      .then(function(data) {
        if (data.success) {
          console.log('PaymentGuard: Session logged successfully - Log ID:', data.logId);
          console.log('PaymentGuard: Scripts detected - Total:', allScripts.length, 'External:', externalScripts.length, 'Payment:', paymentScripts.length);
          if (data.unauthorizedCount > 0) {
            console.warn('PaymentGuard: Session detected', data.unauthorizedCount, 'unauthorized scripts');
          }
        }
      })
      .catch(console.error);
    },

    // UTILITY METHODS
    captureCurrentScriptSignature: function() {
      return Array.from(document.querySelectorAll('script[src]')).map(function(script) {
        return script.src;
      }).sort().join('|');
    },

    captureCurrentScriptList: function() {
      return Array.from(document.querySelectorAll('script[src]')).map(function(script) {
        return script.src;
      });
    },

    // NEW: Capture security headers for monitoring logs
    captureSecurityHeaders: function() {
      var headers = {};
      
      // Try to get CSP from meta tags
      var cspMeta = document.querySelector('meta[http-equiv="Content-Security-Policy"]');
      if (cspMeta) {
        headers['Content-Security-Policy'] = cspMeta.content;
      }

      // Add page metadata
      headers['X-Page-Title'] = document.title;
      headers['X-Page-URL'] = window.location.href;
      headers['X-Referrer'] = document.referrer;
      headers['X-Timestamp'] = new Date().toISOString();

      return headers;
    },

    isPaymentMethodField: function(element) {
      var name = (element.name || '').toLowerCase();
      var id = (element.id || '').toLowerCase();
      return element.type === 'radio' && 
             (name.includes('payment') || id.includes('payment'));
    },

    isPaymentField: function(element) {
      var combined = ((element.name || '') + (element.id || '') + (element.placeholder || '')).toLowerCase();
      return /card|cvv|expire|security|payment|billing/i.test(combined);
    },

    isPaymentScript: function(src) {
      // Enhanced payment script detection including Cardknox
      var paymentPatterns = window.PaymentGuardConfig.paymentProviders;
      
      return paymentPatterns.some(function(pattern) {
        return src.toLowerCase().includes(pattern);
      });
    },

    // NEW: Check if script is local/internal and should be skipped
    isLocalScript: function(src) {
      // Check for relative URLs
      if (src.startsWith('/') || src.startsWith('~/')) {
        return true;
      }
      
      // Check for same origin
      if (src.includes(window.location.origin)) {
        return true;
      }
      
      // Check for localhost and local IPs
      if (src.includes('localhost') || src.includes('127.0.0.1') || src.includes('::1')) {
        return true;
      }
      
      // Skip PaymentGuard's own scripts
      if (src.includes('PaymentGuard') || src.includes('paymentguard')) {
        return true;
      }
      
      // Skip common local libraries
      var localLibraryPatterns = window.PaymentGuardConfig.localLibraryPatterns;
      
      return localLibraryPatterns.some(function(pattern) {
        return src.includes(pattern);
      });
    }
  };

  // Utility function
  function generateSessionId() {
    return 'pg-enhanced-' + Date.now() + '-' + Math.random().toString(36).substr(2, 9);
  }

  // EXISTING auto-initialization - preserved
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', function () {
      PaymentGuard.init();
    });
  } else {
    PaymentGuard.init();
  }

  // EXISTING global exposure - preserved
  window.PaymentGuard = PaymentGuard;

})(window, document);