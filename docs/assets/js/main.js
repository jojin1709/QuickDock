// QuickDock Website Scripts
document.addEventListener('DOMContentLoaded', () => {
  // Mobile navigation toggle
  const mobileToggle = document.getElementById('mobileMenuToggle');
  const navLinks = document.getElementById('navLinks');
  if (mobileToggle && navLinks) {
    mobileToggle.addEventListener('click', () => {
      navLinks.classList.toggle('active');
    });
  }

  // Interactive Notch Demo
  const notchPreview = document.getElementById('interactiveNotch');
  const restingContent = document.getElementById('notchRestingContent');
  const expandedContent = document.getElementById('notchExpandedContent');
  const tabResting = document.getElementById('tabResting');
  const tabHover = document.getElementById('tabHover');

  function setNotchMode(mode) {
    if (!notchPreview) return;
    if (mode === 'hover') {
      notchPreview.classList.add('notch-hover-state');
      if (tabResting) tabResting.classList.remove('active');
      if (tabHover) tabHover.classList.add('active');
      if (restingContent) restingContent.style.display = 'none';
      if (expandedContent) expandedContent.style.display = 'block';
    } else {
      notchPreview.classList.remove('notch-hover-state');
      if (tabResting) tabResting.classList.add('active');
      if (tabHover) tabHover.classList.remove('active');
      if (restingContent) restingContent.style.display = 'flex';
      if (expandedContent) expandedContent.style.display = 'none';
    }
  }

  if (tabResting && tabHover) {
    tabResting.addEventListener('click', () => setNotchMode('resting'));
    tabHover.addEventListener('click', () => setNotchMode('hover'));
  }

  if (notchPreview) {
    notchPreview.addEventListener('mouseenter', () => setNotchMode('hover'));
    notchPreview.addEventListener('mouseleave', () => setNotchMode('resting'));
  }

  // FAQ Accordion
  const faqItems = document.querySelectorAll('.faq-item');
  faqItems.forEach(item => {
    const question = item.querySelector('.faq-question');
    if (question) {
      question.addEventListener('click', () => {
        const isOpen = item.classList.contains('active');
        faqItems.forEach(i => i.classList.remove('active'));
        if (!isOpen) {
          item.classList.add('active');
        }
      });
    }
  });

  // Copy buttons
  const copyButtons = document.querySelectorAll('.copy-btn');
  copyButtons.forEach(btn => {
    btn.addEventListener('click', () => {
      const targetId = btn.getAttribute('data-clipboard-target');
      const targetElem = document.getElementById(targetId);
      if (targetElem) {
        navigator.clipboard.writeText(targetElem.innerText.trim()).then(() => {
          const original = btn.innerText;
          btn.innerText = 'Copied!';
          setTimeout(() => { btn.innerText = original; }, 2000);
        });
      }
    });
  });
});
