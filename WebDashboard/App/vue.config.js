module.exports = {
  publicPath: '/App/',
  outputDir: '../../Run/DashboardDist/App',
  configureWebpack: {
    performance: { hints: false }
  },
  transpileDependencies: [
    'vuetify'
  ]
}