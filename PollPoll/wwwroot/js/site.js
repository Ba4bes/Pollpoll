// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Loading state management for async operations (T106)
(function() {
    'use strict';

    // Show loading spinner for forms on submit (>500ms operations)
    document.addEventListener('DOMContentLoaded', function() {
        // Add loading indicators to all forms
        const forms = document.querySelectorAll('form');
        
        forms.forEach(form => {
            form.addEventListener('submit', function(e) {
                const submitButton = form.querySelector('button[type="submit"]');
                
                if (submitButton && !submitButton.disabled) {
                    // Disable button to prevent double-submit
                    submitButton.disabled = true;
                    
                    // Store original text
                    const originalText = submitButton.innerHTML;
                    
                    // Show loading state after 500ms
                    const loadingTimeout = setTimeout(() => {
                        submitButton.innerHTML = '<span class="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true"></span>Loading...';
                        submitButton.setAttribute('aria-busy', 'true');
                    }, 500);
                    
                    // Clean up if form validation fails or navigates away
                    form.addEventListener('invalid', () => {
                        clearTimeout(loadingTimeout);
                        submitButton.disabled = false;
                        submitButton.innerHTML = originalText;
                        submitButton.removeAttribute('aria-busy');
                    }, { once: true });
                }
            });
        });

        // Keyboard navigation improvements
        // Ensure radio/checkbox inputs can be navigated with arrow keys
        const optionGroups = document.querySelectorAll('.options-list');
        optionGroups.forEach(group => {
            const inputs = Array.from(group.querySelectorAll('input[type="radio"], input[type="checkbox"]'));
            
            inputs.forEach((input, index) => {
                input.addEventListener('keydown', function(e) {
                    if (e.key === 'ArrowDown' || e.key === 'ArrowRight') {
                        e.preventDefault();
                        const nextIndex = (index + 1) % inputs.length;
                        inputs[nextIndex].focus();
                        if (input.type === 'radio') {
                            inputs[nextIndex].checked = true;
                        }
                    } else if (e.key === 'ArrowUp' || e.key === 'ArrowLeft') {
                        e.preventDefault();
                        const prevIndex = (index - 1 + inputs.length) % inputs.length;
                        inputs[prevIndex].focus();
                        if (input.type === 'radio') {
                            inputs[prevIndex].checked = true;
                        }
                    }
                });
            });
        });
    });

    // Announce page navigation to screen readers
    const announcePageChange = function(message) {
        const announcement = document.createElement('div');
        announcement.setAttribute('role', 'status');
        announcement.setAttribute('aria-live', 'polite');
        announcement.className = 'visually-hidden';
        announcement.textContent = message;
        document.body.appendChild(announcement);
        
        setTimeout(() => announcement.remove(), 1000);
    };

    // Export for use in other scripts
    window.PollPoll = window.PollPoll || {};
    window.PollPoll.announcePageChange = announcePageChange;
})();

