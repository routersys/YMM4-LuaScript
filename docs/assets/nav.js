(function () {
  var NAV = [
    {
      group: "はじめに",
      items: [
        { title: "概要", href: "index.html" },
        { title: "導入と使い方", href: "getting-started.html" }
      ]
    },
    {
      group: "Lua 言語",
      items: [
        { title: "Lua の基礎", href: "lua-basics.html" },
        { title: "標準ライブラリ", href: "stdlib.html" }
      ]
    },
    {
      group: "API リファレンス",
      items: [
        { title: "グローバル変数", href: "globals.html" },
        { title: "obj オブジェクト", href: "obj.html" },
        { title: "obj の関数", href: "obj-functions.html" },
        { title: "anim ライブラリ", href: "anim.html" },
        { title: "scene と ymm4", href: "scene-ymm4.html" },
        { title: "ピクセル操作", href: "pixels.html" }
      ]
    },
    {
      group: "高度な機能",
      items: [
        { title: "エンジンと高速化", href: "engines.html" },
        { title: "パラメータ指定子", href: "directives.html" }
      ]
    },
    {
      group: "実用",
      items: [
        { title: "サンプル集", href: "samples.html" },
        { title: "クイックリファレンス", href: "reference.html" },
        { title: "制限事項とトラブル", href: "limits.html" }
      ]
    }
  ];

  var titleOf = {};
  for (var gi = 0; gi < NAV.length; gi++) {
    for (var ii = 0; ii < NAV[gi].items.length; ii++) {
      titleOf[NAV[gi].items[ii].href] = NAV[gi].items[ii].title;
    }
  }

  var indexPromise = null;
  var searchTimer = null;

  function currentFile() {
    var path = window.location.pathname;
    var file = path.substring(path.lastIndexOf("/") + 1);
    return file === "" ? "index.html" : file;
  }

  function escapeHtml(text) {
    return text
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;");
  }

  function build() {
    var here = currentFile();
    var sidebar = document.getElementById("sidebar");
    if (!sidebar) return;

    var html = '<a class="brand" href="index.html">Lua スクリプト for YMM4</a>';
    html += '<input id="navfilter" type="search" placeholder="全ページを検索" aria-label="全ページを検索" autocomplete="off">';
    html += '<div id="navresults"></div>';
    html += '<div id="navtree">';
    for (var i = 0; i < NAV.length; i++) {
      html += '<div class="group">' + NAV[i].group + "</div><ul>";
      for (var j = 0; j < NAV[i].items.length; j++) {
        var it = NAV[i].items[j];
        var cls = it.href === here ? ' class="active"' : "";
        html += "<li><a" + cls + ' href="' + it.href + '">' + it.title + "</a></li>";
      }
      html += "</ul>";
    }
    html += "</div>";
    sidebar.innerHTML = html;

    var filter = document.getElementById("navfilter");
    filter.addEventListener("input", function () {
      var q = filter.value.trim();
      if (searchTimer) clearTimeout(searchTimer);
      searchTimer = setTimeout(function () { runSearch(q); }, 150);
    });
  }

  function runSearch(query) {
    var tree = document.getElementById("navtree");
    var results = document.getElementById("navresults");
    if (!tree || !results) return;

    if (query.length < 2) {
      results.innerHTML = "";
      tree.style.display = "";
      return;
    }

    tree.style.display = "none";
    results.innerHTML = '<div class="navmsg">検索中…</div>';

    buildIndex().then(function (sections) {
      if (document.getElementById("navfilter").value.trim() !== query) return;
      render(results, sections, query);
    });
  }

  function buildIndex() {
    if (indexPromise) return indexPromise;

    var hrefs = [];
    for (var key in titleOf) {
      if (Object.prototype.hasOwnProperty.call(titleOf, key)) hrefs.push(key);
    }

    indexPromise = Promise.all(hrefs.map(fetchSections)).then(function (lists) {
      var all = [];
      for (var i = 0; i < lists.length; i++) {
        for (var j = 0; j < lists[i].length; j++) all.push(lists[i][j]);
      }
      return all;
    });
    return indexPromise;
  }

  function fetchSections(href) {
    return fetch(href)
      .then(function (res) { return res.text(); })
      .then(function (text) {
        var doc = new DOMParser().parseFromString(text, "text/html");
        var main = doc.querySelector("main");
        if (!main) return [];

        var sections = [];
        var heading = "";
        var buffer = "";
        var children = main.children;

        function flush() {
          var body = buffer.replace(/\s+/g, " ").trim();
          if (body.length > 0) {
            sections.push({
              href: href,
              page: titleOf[href] || href,
              heading: heading,
              text: body,
              lower: body.toLowerCase()
            });
          }
          buffer = "";
        }

        for (var i = 0; i < children.length; i++) {
          var el = children[i];
          var tag = el.tagName;
          if (tag === "H2" || tag === "H3") {
            flush();
            heading = el.textContent.replace(/\s+/g, " ").trim();
          } else if (el.className === "pager") {
            continue;
          } else {
            buffer += " " + el.textContent;
          }
        }
        flush();
        return sections;
      })
      .catch(function () { return []; });
  }

  function render(container, sections, query) {
    var q = query.toLowerCase();
    var hits = [];
    for (var i = 0; i < sections.length; i++) {
      var s = sections[i];
      var inHeading = s.heading.toLowerCase().indexOf(q) !== -1;
      var inPage = s.page.toLowerCase().indexOf(q) !== -1;
      var pos = s.lower.indexOf(q);
      if (inHeading || inPage || pos !== -1) {
        hits.push({ section: s, pos: pos, rank: inHeading || inPage ? 0 : 1 });
      }
    }

    hits.sort(function (a, b) { return a.rank - b.rank; });

    if (hits.length === 0) {
      container.innerHTML = '<div class="navmsg">「' + escapeHtml(query) + "」に一致する内容はありません。</div>";
      return;
    }

    var max = Math.min(hits.length, 40);
    var html = '<div class="navmsg">' + hits.length + " 件</div>";
    for (var k = 0; k < max; k++) {
      var s2 = hits[k].section;
      var label = s2.heading ? s2.page + " — " + s2.heading : s2.page;
      var anchor = s2.heading ? "#:~:text=" + encodeURIComponent(s2.heading) : "";
      html +=
        '<a class="navhit" href="' + s2.href + anchor + '">' +
        '<span class="h">' + escapeHtml(label) + "</span>" +
        '<span class="s">' + snippet(s2.text, hits[k].pos, query) + "</span>" +
        "</a>";
    }
    container.innerHTML = html;
  }

  function snippet(text, pos, query) {
    if (pos < 0) {
      return escapeHtml(text.substring(0, 70)) + (text.length > 70 ? "…" : "");
    }
    var start = Math.max(0, pos - 30);
    var end = Math.min(text.length, pos + query.length + 40);
    var before = (start > 0 ? "…" : "") + text.substring(start, pos);
    var hit = text.substring(pos, pos + query.length);
    var after = text.substring(pos + query.length, end) + (end < text.length ? "…" : "");
    return escapeHtml(before) + "<mark>" + escapeHtml(hit) + "</mark>" + escapeHtml(after);
  }

  function toggle() {
    var btn = document.getElementById("navtoggle");
    if (!btn) return;
    btn.addEventListener("click", function () {
      document.body.classList.toggle("nav-open");
    });
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", function () { build(); toggle(); });
  } else {
    build();
    toggle();
  }
})();
